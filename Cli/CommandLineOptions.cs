namespace Lsof.Cli;

internal sealed class CommandLineOptions
{
    private const string ProcessOptionPrefix = "--process=";
    private const string PortOptionPrefix = "--port=";
    private const string CommandOptionPrefix = "--command=";
    private const string UserOptionPrefix = "--user=";
    private const string NetworkSwitchPrefix = "-i";

    public List<string> ProcessFilters { get; } = [];
    public List<string> UserFilters { get; } = [];
    public List<string> PathFilters { get; } = [];
    public List<int> PortFilters { get; } = [];
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
                    // Unix form: "lsof -i :22" passes the port spec as a separate argument.
                    if (
                        arg == NetworkSwitchPrefix
                        && i + 1 < args.Length
                        && NetworkSpecParser.LooksLikeNetworkSpec(args[i + 1])
                    )
                    {
                        if (!NetworkSpecParser.ParseNetworkSpec(options.PortFilters, args[++i]))
                            return false;
                    }
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
                case "--unique":
                    options.Unique = true;
                    break;
                case "-j":
                case "--json":
                    options.Json = true;
                    break;
                case "-p":
                case "--process":
                case "-c":
                case "--command":
                    if (!TryReadSeparateValue(args, ref i, out string processValue))
                        return false;
                    if (!AddStringFilters(options.ProcessFilters, processValue))
                        return false;
                    break;
                case "-u":
                case "--user":
                    if (!TryReadSeparateValue(args, ref i, out string userValue))
                        return false;
                    if (!AddStringFilters(options.UserFilters, userValue))
                        return false;
                    break;
                case "-P":
                case "--port":
                    if (!TryReadSeparateValue(args, ref i, out string portValue))
                        return false;
                    if (!AddPortFilters(options, portValue))
                        return false;
                    break;
                default:
                    if (!TryParseDefault(options, arg))
                        return false;
                    break;
            }
        }
        return true;
    }

    private static bool TryParseDefault(CommandLineOptions options, string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            return false;
        }

        if (arg.StartsWith(ProcessOptionPrefix, StringComparison.Ordinal))
        {
            return AddStringFilters(options.ProcessFilters, arg[ProcessOptionPrefix.Length..]);
        }

        if (arg.StartsWith(CommandOptionPrefix, StringComparison.Ordinal))
        {
            return AddStringFilters(options.ProcessFilters, arg[CommandOptionPrefix.Length..]);
        }

        if (arg.StartsWith(UserOptionPrefix, StringComparison.Ordinal))
        {
            return AddStringFilters(options.UserFilters, arg[UserOptionPrefix.Length..]);
        }

        if (arg.StartsWith(PortOptionPrefix, StringComparison.Ordinal))
        {
            return AddPortFilters(options, arg[PortOptionPrefix.Length..]);
        }

        // Unix form: "lsof -i:22" attaches the port spec directly to the switch.
        if (
            arg.StartsWith(NetworkSwitchPrefix, StringComparison.Ordinal)
            && arg.Length > NetworkSwitchPrefix.Length
        )
        {
            options.Network = true;
            return NetworkSpecParser.ParseNetworkSpec(
                options.PortFilters,
                arg[NetworkSwitchPrefix.Length..]
            );
        }

        // Any other non-option token is treated as a file path, matching "lsof /path/to/file".
        if (!IsOptionToken(arg))
        {
            options.PathFilters.Add(arg);
            return true;
        }

        return false;
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

    private static bool AddStringFilters(List<string> target, string value)
    {
        int initialCount = target.Count;
        ReadOnlySpan<char> input = value.AsSpan();
        foreach (Range range in input.Split(','))
        {
            ReadOnlySpan<char> part = input[range].Trim();
            if (!part.IsEmpty)
            {
                target.Add(part.ToString());
            }
        }

        return target.Count > initialCount;
    }

    private static bool AddPortFilters(CommandLineOptions options, string value)
    {
        int initialCount = options.PortFilters.Count;
        ReadOnlySpan<char> input = value.AsSpan();
        foreach (Range range in input.Split(','))
        {
            ReadOnlySpan<char> part = input[range].Trim();
            if (part.IsEmpty)
                continue;
            if (!NetworkSpecParser.TryAddPort(options.PortFilters, part))
                return false;
        }

        return options.PortFilters.Count > initialCount;
    }
}
