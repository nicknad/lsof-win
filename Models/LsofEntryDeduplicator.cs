namespace Lsof.Models;

internal static class LsofEntryDeduplicator
{
    private readonly record struct EntryKey(
        int ProcessId,
        string ProcessName,
        string ProcessPath,
        LsofEntryKind Kind,
        NetworkAddressFamily? AddressFamily,
        string Protocol,
        string LocalAddress,
        int LocalPort,
        string RemoteAddress,
        int RemotePort,
        string State,
        string Handle,
        string Name)
    {
        public static EntryKey From(LsofEntry entry)
        {
            return new EntryKey(
                entry.ProcessId,
                entry.ProcessName,
                entry.ProcessPath,
                entry.Kind,
                entry.AddressFamily,
                entry.Protocol,
                entry.LocalAddress,
                entry.LocalPort,
                entry.RemoteAddress,
                entry.RemotePort,
                entry.State,
                entry.Handle,
                entry.Name);
        }
    }

    private sealed class EntryKeyComparer : IEqualityComparer<EntryKey>
    {
        private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

        public static readonly EntryKeyComparer Instance = new();

        public bool Equals(EntryKey x, EntryKey y)
        {
            return x.ProcessId == y.ProcessId
                && TextComparer.Equals(x.ProcessName, y.ProcessName)
                && TextComparer.Equals(x.ProcessPath, y.ProcessPath)
                && x.Kind == y.Kind
                && x.AddressFamily == y.AddressFamily
                && TextComparer.Equals(x.Protocol, y.Protocol)
                && TextComparer.Equals(x.LocalAddress, y.LocalAddress)
                && x.LocalPort == y.LocalPort
                && TextComparer.Equals(x.RemoteAddress, y.RemoteAddress)
                && x.RemotePort == y.RemotePort
                && TextComparer.Equals(x.State, y.State)
                && TextComparer.Equals(x.Handle, y.Handle)
                && TextComparer.Equals(x.Name, y.Name);
        }

        public int GetHashCode(EntryKey key)
        {
            HashCode hash = new();
            hash.Add(key.ProcessId);
            hash.Add(key.ProcessName, TextComparer);
            hash.Add(key.ProcessPath, TextComparer);
            hash.Add(key.Kind);
            hash.Add(key.AddressFamily);
            hash.Add(key.Protocol, TextComparer);
            hash.Add(key.LocalAddress, TextComparer);
            hash.Add(key.LocalPort);
            hash.Add(key.RemoteAddress, TextComparer);
            hash.Add(key.RemotePort);
            hash.Add(key.State, TextComparer);
            hash.Add(key.Handle, TextComparer);
            hash.Add(key.Name, TextComparer);
            return hash.ToHashCode();
        }
    }

    public static List<LsofEntry> RemoveDuplicateEntries(IReadOnlyList<LsofEntry> entries)
    {
        HashSet<EntryKey> seen = new(EntryKeyComparer.Instance);
        List<LsofEntry> result = new(entries.Count);

        foreach (LsofEntry entry in entries)
        {
            if (seen.Add(EntryKey.From(entry)))
            {
                result.Add(entry);
            }
        }
        return result;
    }
}
