using Lsof.Models;

namespace Lsof.Output;

internal static class TableFormatter
{
    private static readonly string[] HeaderValues = { "Command", "PID", "Type", "FD", "Proto", "Local", "Remote", "State", "Name" };

    public static ReadOnlySpan<string> Headers => HeaderValues;

    public static string[] GetCells(LsofEntry entry)
    {
        return new[]
        {
            entry.ProcessName,
            entry.ProcessId.ToString(),
            entry.Category,
            entry.Handle,
            entry.Protocol,
            FormatEndpoint(entry.LocalAddress, entry.LocalPort),
            FormatEndpoint(entry.RemoteAddress, entry.RemotePort),
            entry.State,
            entry.Name
        };
    }

    public static string FormatEndpoint(string address, int port)
    {
        if (string.IsNullOrEmpty(address) || port == 0) // Port 0 marks an absent or unspecified endpoint here.
        {
            return "";
        }

        return address.Contains(':')
            ? $"[{address}]:{port}"
            : $"{address}:{port}";
    }
}
