using System.Diagnostics;
using Lsof.Processes;

namespace Lsof.Tests.Support;

internal static class TestCatalogFactory
{
    public static ProcessCatalog Create(params ProcessInfo[] processInfos)
    {
        Dictionary<int, ProcessInfo> byId = processInfos.ToDictionary(info => info.ProcessId);
        return new ProcessCatalog(Array.Empty<Process>(), byId);
    }

    public static ProcessCatalog CreateWithProcess(Process process)
    {
        int processId = process.Id;
        Dictionary<int, ProcessInfo> byId = new()
        {
            [processId] = new ProcessInfo(processId, process.ProcessName, "")
        };
        return new ProcessCatalog([process], byId);
    }
}
