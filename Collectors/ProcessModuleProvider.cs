using System.Diagnostics;
using Lsof.Processes;

namespace Lsof.Collectors;

internal sealed class ProcessModuleProvider : IProcessModuleProvider
{
    public ModuleSnapshot GetModules(Process process, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Process.Modules asks the OS for the target process's loaded module list; enumerating it
        // can throw for exited or protected processes.
        ProcessModule[] modules = SnapshotModules(process.Modules, cancellationToken);
        List<string> paths = new(modules.Length);
        int unreadablePathCount = 0;

        foreach (ProcessModule module in modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // ProcessModule.FileName is another OS read (the module's path) that can fail per module.
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
            // Indexing ProcessModuleCollection reads one native module entry at a time.
            snapshot[index] = modules[index];
        }

        return snapshot;
    }
}
