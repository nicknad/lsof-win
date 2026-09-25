using System.Diagnostics;
using Lsof.Models;
using Lsof.Processes;

namespace Lsof.Collectors;

internal static class ModuleCollector
{
    private static readonly IProcessModuleProvider NativeModuleProvider = new ProcessModuleProvider();

    public static void Collect(
        ProcessCatalog catalog,
        ProcessSelection selection,
        List<LsofEntry> entries,
        CollectionReport report,
        CancellationToken cancellationToken = default)
    {
        Collect(catalog, selection, entries, report, NativeModuleProvider, cancellationToken);
    }

    internal static void Collect(
        ProcessCatalog catalog,
        ProcessSelection selection,
        List<LsofEntry> entries,
        CollectionReport report,
        IProcessModuleProvider moduleProvider,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (selection.IsEmpty)
        {
            return;
        }

        int processInspectionFailures = 0;
        int moduleCollectionFailures = 0;
        int modulePathFailures = 0;
        foreach (Process process in catalog.Processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int processId;
            try
            {
                processId = process.Id;
            }
            catch (Exception exception) when (ProcessCatalog.IsExpectedProcessInspectionException(process, exception))
            {
                processInspectionFailures++;
                continue;
            }

            if (!selection.Includes(processId))
            {
                continue;
            }

            ModuleSnapshot modules;
            try
            {
                modules = moduleProvider.GetModules(process, cancellationToken);
            }
            catch (Exception exception) when (ProcessCatalog.IsExpectedProcessInspectionException(process, exception))
            {
                moduleCollectionFailures++;
                continue;
            }

            modulePathFailures += modules.UnreadablePathCount;
            foreach (string modulePath in modules.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                LsofEntry entry = catalog.CreateEntry(processId);
                entry.Kind = LsofEntryKind.Module;
                entry.Name = modulePath;
                entries.Add(entry);
            }
        }

        int failureCount = processInspectionFailures + moduleCollectionFailures + modulePathFailures;
        if (failureCount > 0)
        {
            report.AddWarning(
                $"Warning: module inspection was incomplete ({processInspectionFailures} process IDs, " +
                $"{moduleCollectionFailures} module lists, {modulePathFailures} module paths could not be read).");
        }
    }
}
