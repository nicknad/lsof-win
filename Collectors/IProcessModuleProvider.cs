using System.Diagnostics;

namespace Lsof.Collectors;

internal sealed record ModuleSnapshot(string[] Paths, int UnreadablePathCount);

internal interface IProcessModuleProvider
{
    ModuleSnapshot GetModules(Process process, CancellationToken cancellationToken);
}
