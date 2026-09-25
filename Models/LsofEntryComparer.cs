namespace Lsof.Models;

internal sealed class LsofEntryComparer : IComparer<LsofEntry>
{
    public static readonly LsofEntryComparer Instance = new();

    private LsofEntryComparer()
    {
    }

    public int Compare(LsofEntry? x, LsofEntry? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int result = CompareText(x.ProcessName, y.ProcessName);
        if (result != 0) return result;

        result = x.ProcessId.CompareTo(y.ProcessId);
        if (result != 0) return result;

        result = x.Kind.CompareTo(y.Kind);
        if (result != 0) return result;

        result = Nullable.Compare(x.AddressFamily, y.AddressFamily);
        if (result != 0) return result;

        result = x.LocalPort.CompareTo(y.LocalPort);
        if (result != 0) return result;

        result = CompareText(x.LocalAddress, y.LocalAddress);
        if (result != 0) return result;

        result = CompareText(x.Protocol, y.Protocol);
        if (result != 0) return result;

        result = x.RemotePort.CompareTo(y.RemotePort);
        if (result != 0) return result;

        result = CompareText(x.RemoteAddress, y.RemoteAddress);
        if (result != 0) return result;

        result = CompareText(x.State, y.State);
        if (result != 0) return result;

        result = CompareText(x.Name, y.Name);
        if (result != 0) return result;

        result = CompareText(x.Handle, y.Handle);
        if (result != 0) return result;

        return CompareText(x.ProcessPath, y.ProcessPath);
    }

    private static int CompareText(string? left, string? right)
    {
        int result = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        return result != 0 ? result : string.Compare(left, right, StringComparison.Ordinal);
    }
}
