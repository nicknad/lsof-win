namespace Lsof.Models;

internal readonly struct LsofSummary
{
    public int Connections { get; init; }
    public int Files { get; init; }
    public int Modules { get; init; }

    public static LsofSummary FromEntries(IReadOnlyList<LsofEntry> entries)
    {
        int connections = 0;
        int files = 0;
        int modules = 0;

        foreach (LsofEntry entry in entries)
        {
            switch (entry.Kind)
            {
                case LsofEntryKind.Connection:
                    connections++;
                    break;
                case LsofEntryKind.File:
                    files++;
                    break;
                case LsofEntryKind.Module:
                    modules++;
                    break;
            }
        }

        return new LsofSummary
        {
            Connections = connections,
            Files = files,
            Modules = modules
        };
    }
}
