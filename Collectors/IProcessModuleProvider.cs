using System.Diagnostics;

namespace Lsof.Collectors;

internal interface IProcessModuleProvider
{
    ModuleSnapshot GetModules(Process process, CancellationToken cancellationToken);
}
