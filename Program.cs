using Lsof.Cli;
using Lsof.Collectors;
using Lsof.Models;
using Lsof.Native;
using Lsof.Output;
using Lsof.Processes;

namespace Lsof;

internal static class Program
{
    private const int InvalidArgumentsExitCode = 1;
    private const int IncompleteCollectionExitCode = 2;
    private const int CancelledExitCode = 130;

    private static int Main(string[] args)
    {
        using CancellationTokenSource cancellation = new();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            return Run(args, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    internal static int Run(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            return RunCore(args, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine("Cancelled.");
            return CancelledExitCode;
        }
    }

    private static int RunCore(string[] args, CancellationToken cancellationToken)
    {
        if (!CommandLineOptions.TryParse(args, out CommandLineOptions options, out bool showHelp))
        {
            Console.Error.WriteLine("Invalid arguments. Use --help for usage.");
            return InvalidArgumentsExitCode;
        }

        if (showHelp)
        {
            HelpPrinter.Print();
            return 0;
        }

        ApplyDefaultCollectors(options);

        CollectionReport report = new();
        using ProcessCatalog catalog = ProcessCatalog.Create(report, cancellationToken);
        ProcessFilter filter = new(options.ProcessFilters);
        ProcessSelection selection = filter.ResolveSelection(catalog, report, cancellationToken);

        List<LsofEntry> entries = new();
        if (options.Network)
        {
            NetworkCollector.Collect(catalog, selection, options.PortFilters, entries, report, cancellationToken);
        }
        if (options.Files)
        {
            if (!selection.IsEmpty)
            {
                Console.Error.WriteLine("Enumerating open file handles...");
            }

            FileHandleCollector.Collect(
                catalog,
                selection,
                entries,
                report,
                progress => Console.Error.WriteLine(
                    $"Enumerated {progress.ProcessedHandles:N0}/{progress.TotalHandles:N0} handles; found {progress.FilesFound:N0} disk files."),
                cancellationToken);
        }
        if (options.Modules)
        {
            ModuleCollector.Collect(catalog, selection, entries, report, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (options.Unique)
        {
            entries = LsofEntryDeduplicator.RemoveDuplicateEntries(entries);
        }
        entries.Sort(LsofEntryComparer.Instance);

        if (options.Json)
        {
            JsonOutputWriter.Write(entries);
            WriteDiagnostics(report);
            return GetExitCode(report);
        }

        if (entries.Count == 0)
        {
            Console.WriteLine("No matching open files, connections, or modules.");
            WriteDiagnostics(report);
            return GetExitCode(report);
        }

        TableOutputWriter.Write(entries);

        LsofSummary summary = LsofSummary.FromEntries(entries);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{summary.Connections} connection(s), {summary.Files} file handle(s), {summary.Modules} module(s)");
        Console.ResetColor();
        WriteDiagnostics(report);
        return GetExitCode(report);
    }

    internal static int GetExitCode(CollectionReport report)
    {
        return report.HasWarnings ? IncompleteCollectionExitCode : 0;
    }

    internal static void ApplyDefaultCollectors(CommandLineOptions options)
    {
        if (!options.Network && !options.Files && !options.Modules)
        {
            options.Network = true;
            options.Files = true;
        }
    }

    private static void WriteDiagnostics(CollectionReport report)
    {
        foreach (CollectionDiagnostic diagnostic in report.Diagnostics)
        {
            Console.Error.WriteLine(diagnostic.Message);
        }
    }
}
