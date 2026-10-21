using Lsof.Cli;
using Lsof.Collectors;
using Lsof.Models;
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
        // Console.CancelKeyPress fires on Ctrl+C; setting eventArgs.Cancel keeps the process alive so
        // the collectors can stop cooperatively through the token and return the cancelled exit code.
        void CancelHandler(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += CancelHandler;

        try
        {
            return Run(args, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= CancelHandler;
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
        ProcessFilter filter = new(
            options.ProcessFilters,
            options.UserFilters,
            CreateOwnerProvider(options)
        );
        ProcessSelection selection = filter.ResolveSelection(catalog, report, cancellationToken);

        List<LsofEntry> entries = [];
        if (options.Network)
        {
            NetworkCollector.Collect(
                catalog,
                selection,
                options.PortFilters,
                entries,
                report,
                cancellationToken
            );
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
                new FileFilter(options.PathFilters),
                entries,
                report,
                progress =>
                    Console.Error.WriteLine(
                        $"Enumerated {progress.ProcessedHandles:N0}/{progress.TotalHandles:N0} handles; found {progress.FilesFound:N0} disk files."
                    ),
                cancellationToken
            );
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
        }
        else if (entries.Count == 0)
        {
            Console.WriteLine("No matching open files, connections, or modules.");
        }
        else
        {
            TableOutputWriter.Write(entries);

            LsofSummary summary = LsofSummary.FromEntries(entries);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                $"{summary.Connections} connection(s), {summary.Files} file handle(s), {summary.Modules} module(s)"
            );
            Console.ResetColor();
        }

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
            // A positional path filter targets file handles, so default to files only.
            if (options.PathFilters.Count > 0)
            {
                options.Files = true;
                return;
            }

            options.Network = true;
            options.Files = true;
        }
    }

    // Only resolves owners when a user filter is present, so the common path pays no extra cost.
    private static ProcessOwnerProvider? CreateOwnerProvider(CommandLineOptions options)
    {
        return options.UserFilters.Count > 0 ? new ProcessOwnerProvider() : null;
    }

    private static void WriteDiagnostics(CollectionReport report)
    {
        foreach (CollectionDiagnostic diagnostic in report.Diagnostics)
        {
            Console.Error.WriteLine(diagnostic.Message);
        }
    }
}
