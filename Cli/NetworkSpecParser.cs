using System.Globalization;

namespace Lsof.Cli;

// Parses the Unix-style `-i` port specs (`-i:22`, `-i :22`, `-i tcp:443`).
// Extracted from CommandLineOptions so option dispatch stays separate from this sub-grammar.
internal static class NetworkSpecParser
{
    private const int MinimumPort = 0;
    private const int MaximumPort = ushort.MaxValue; // TCP/UDP ports are unsigned 16-bit values.

    internal static bool LooksLikeNetworkSpec(string value)
    {
        if (string.IsNullOrEmpty(value) || value == "/?")
        {
            return false;
        }

        ReadOnlySpan<char> input = value.AsSpan();
        bool sawSegment = false;
        foreach (Range range in input.Split(','))
        {
            ReadOnlySpan<char> part = input[range].Trim();
            if (part.IsEmpty)
                continue;
            if (!IsNetworkSpecSegment(part))
                return false;
            sawSegment = true;
        }

        return sawSegment;
    }

    internal static bool ParseNetworkSpec(List<int> portFilters, string value)
    {
        ReadOnlySpan<char> input = value.AsSpan();
        foreach (Range range in input.Split(','))
        {
            ReadOnlySpan<char> part = input[range].Trim();
            part = StripProtocolPrefix(part);
            if (part.IsEmpty)
                continue;
            if (part[0] == ':')
                part = part[1..];
            if (part.IsEmpty)
                continue;
            if (!TryAddPort(portFilters, part))
                return false;
        }

        return true;
    }

    internal static bool TryAddPort(List<int> portFilters, ReadOnlySpan<char> value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
            return false;
        if (port is < MinimumPort or > MaximumPort)
            return false;
        portFilters.Add(port);
        return true;
    }

    private static bool IsNetworkSpecSegment(ReadOnlySpan<char> part)
    {
        if (part[0] == ':')
        {
            return true;
        }

        if (
            part.StartsWith("tcp", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("udp", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        foreach (char c in part)
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }

        return true;
    }

    private static ReadOnlySpan<char> StripProtocolPrefix(ReadOnlySpan<char> value)
    {
        if (
            value.StartsWith("tcp", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("udp", StringComparison.OrdinalIgnoreCase)
        )
        {
            ReadOnlySpan<char> remainder = value[3..];
            return remainder.Length > 0 && remainder[0] == ':' ? remainder[1..] : [];
        }

        return value;
    }
}
