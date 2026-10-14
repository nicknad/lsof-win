namespace Lsof.Cli;

// Single source of truth for CLI documentation.
// HelpPrinter prints Lines verbatim; README.md must contain the same Options block;
// Tests/Cli/CliReferenceTests.cs enforces both so help, docs, and parser cannot drift.
internal static class CliReference
{
    public static readonly string[] Lines =
    [
        "lsof for Windows - list open files, sockets, and loaded modules",
        "",
        "Usage: lsof [options]",
        "",
        "Options:",
        "  -p, --process <name|pid>  Filter by process name (wildcards allowed) or PID",
        "      --process=<name|pid>  Inline form; use when the value begins with '-'",
        "                            Comma-separated and repeatable",
        "  -c, --command <name>      Filter by process (command) name; alias of -p for names",
        "  -u, --user <user>         Filter by owning user (name, domain\\name, or SID)",
        "      --user=<user>         Inline form; comma-separated and repeatable",
        "  -P, --port <port>         Filter network rows by port",
        "      --port=<port>         Inline form",
        "                            Local for UDP; local or remote for TCP",
        "                            Comma-separated and repeatable",
        "  -i, --network             Show TCP/UDP connections",
        "      -i:<port>             Also accepts a port spec: -i:22, -i :22, -i tcp:443",
        "  -f, --files               Show open file handles",
        "  -m, --modules             Show loaded modules (DLLs)",
        "  -a, --all                 Show connections, files, and modules",
        "      --unique              Remove duplicate rows",
        "  -j, --json                Output JSON",
        "  <path>                    Show only handles for the given file path;",
        "                            defaults to --files when no type flag is present",
        "  -h, --help, /?            Show this help",
        "                            Help exits immediately; later options are ignored",
        "",
        "Default (no type flags): --network --files, or --files when a <path> is given",
        "",
        "Exit codes: 0 complete, 1 invalid arguments, 2 incomplete collection, 130 cancelled",
        "",
        "Examples:",
        "  lsof -i -P 443",
        "  lsof -i:22",
        "  lsof -f -p chrome",
        "  lsof -u DOMAIN\\alice",
        "  lsof C:\\Windows\\notepad.exe",
        "  lsof -a -p 1234 --json",
    ];
}
