using System.Globalization;

namespace Lsof.Cli;

internal sealed class CommandLineOptions
{
    private const string ProcessOptionPrefix = "--process=";
    private const string PortOptionPrefix = "--port=";
    private const int MinimumPort = 0;
    private const int MaximumPort = ushort.MaxValue; // TCP/UDP ports are unsigned 16-bit values.

    public List<string> ProcessFilters { get; } = new();
    public List<int> PortFilters { get; } = new();
    public bool Network { get; set; }
    public bool Files { get; set; }
    public bool Modules { get; set; }
    public bool Unique { get; set; }
    public bool Json { get; set; }

    public static bool TryParse(string[] args, out CommandLineOptions options, out bool showHelp)
    {
        options = new CommandLineOptions();
        showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                case "/?":
                    showHelp = true;
                    return true;
                case "-i":
                case "--network":
                    options.Network = true;
                    break;
                case "-f":
                case "--files":
                    options.Files = true;
                    break;
                case "-m":
                case "--modules":
                    options.Modules = true;
                    break;
                case "-a":
                case "--all":
                    options.Network = true;
                    options.Files = true;
                    options.Modules = true;
                    break;
                case "-u":
                case "--unique":
                    options.Unique = true;
                    break;
                case "-j":
                case "--json":
                    options.Json = true;
                    break;
                case "-p":
                case "--process":
                    if (!TryReadSeparateValue(args, ref i, out string processValue)) return false;
                    if (!AddProcessFilters(options, processValue)) return false;
                    break;
                case "-P":
                case "--port":
                    if (!TryReadSeparateValue(args, ref i, out string portValue)) return false;
                    if (!AddPortFilters(options, portValue)) return false;
                    break;
                default:
                    if (arg.StartsWith(ProcessOptionPrefix, StringComparison.Ordinal))
                    {
                        if (!AddProcessFilters(options, arg[ProcessOptionPrefix.Length..])) return false;
                    }
                    else if (arg.StartsWith(PortOptionPrefix, StringComparison.Ordinal))
                    {
                        if (!AddPortFilters(options, arg[PortOptionPrefix.Length..])) return false;
                    }
                    else
                    {
                        return false;
                    }
                    break;
            }
        }
        return true;
    }

    private static bool TryReadSeparateValue(string[] args, ref int index, out string value)
    {
        value = "";
        if (index + 1 >= args.Length || IsOptionToken(args[index + 1]))
        {
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool IsOptionToken(string value)
    {
        return value.StartsWith('-') || value == "/?";
    }

    private static bool AddProcessFilters(CommandLineOptions options, string value)
    {
        int initialCount = options.ProcessFilters.Count;
        ReadOnlySpan<char> input = value.AsSpan();
        foreach (Range range in input.Split(','))
        {
            ReadOnlySpan<char> part = input[range].Trim();
            if (!part.IsEmpty)
            {
                options.ProcessFilters.Add(part.ToString());
            }
        }

        return options.ProcessFilters.Count > initialCount;
    }

    private static bool AddPortFilters(CommandLineOptions options, string value)
    {
        int initialCount = options.PortFilters.Count;
        ReadOnlySpan<char> input = value.AsSpan();
        foreach (Range range in input.Split(','))
        {
            ReadOnlySpan<char> part = input[range].Trim();
            if (part.IsEmpty) continue;
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)) return false;
            if (port < MinimumPort || port > MaximumPort) return false;
            options.PortFilters.Add(port);
        }

        return options.PortFilters.Count > initialCount;
    }
}
