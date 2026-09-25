namespace Lsof.Processes;

internal sealed class ProcessSelection
{
    private readonly HashSet<int> _processIds;

    private ProcessSelection(bool includesAllProcesses, HashSet<int> processIds)
    {
        IncludesAllProcesses = includesAllProcesses;
        _processIds = processIds;
    }

    public static ProcessSelection AllProcesses { get; } = new(true, new HashSet<int>());

    public bool IncludesAllProcesses { get; }

    public IReadOnlySet<int> ProcessIds => _processIds;

    public bool IsEmpty => !IncludesAllProcesses && _processIds.Count == 0;

    public bool Includes(int processId)
    {
        return IncludesAllProcesses || _processIds.Contains(processId);
    }

    public static ProcessSelection FromProcessIds(IEnumerable<int> processIds)
    {
        return new ProcessSelection(false, new HashSet<int>(processIds));
    }
}
