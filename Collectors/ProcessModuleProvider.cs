using System.Diagnostics;
using Lsof.Processes;

namespace Lsof.Collectors;

internal sealed class ProcessModuleProvider : IProcessModuleProvider
{
    public ModuleSnapshot GetModules(Process process, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProcessModule[] modules = SnapshotModules(process.Modules, cancellationToken);
        List<string> paths = new(modules.Length);
        int unreadablePathCount = 0;

        foreach (ProcessModule module in modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                paths.Add(module.FileName ?? "");
            }
            catch (Exception exception) when (ProcessCatalog.IsExpectedProcessInspectionException(process, exception))
            {
                unreadablePathCount++;
            }
        }

        return new ModuleSnapshot(paths.ToArray(), unreadablePathCount);
    }

    internal static ProcessModule[] SnapshotModules(ProcessModuleCollection modules, CancellationToken cancellationToken)
    {
        ProcessModule[] snapshot = new ProcessModule[modules.Count];
        // Snapshot before constructing output rows so later iteration is independent of the live Process object.
        for (int index = 0; index < modules.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            snapshot[index] = modules[index];
        }

        return snapshot;
    }
}
