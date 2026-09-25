namespace Lsof.Cli;

internal static class HelpPrinter
{
    public static void Print()
    {
        Console.WriteLine("lsof for Windows - list open files, sockets, and loaded modules");
        Console.WriteLine();
        Console.WriteLine("Usage: lsof [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -p, --process <name|pid>  Filter by process name (wildcards allowed) or PID");
        Console.WriteLine("      --process=<name|pid>  Inline form; use when the value begins with '-'");
        Console.WriteLine("                            Comma-separated and repeatable");
        Console.WriteLine("  -P, --port <port>         Filter network rows by port");
        Console.WriteLine("      --port=<port>         Inline form");
        Console.WriteLine("                            Local for UDP; local or remote for TCP");
        Console.WriteLine("                            Comma-separated and repeatable");
        Console.WriteLine("  -i, --network             Show TCP/UDP connections");
        Console.WriteLine("  -f, --files               Show open file handles");
        Console.WriteLine("  -m, --modules             Show loaded modules (DLLs)");
        Console.WriteLine("  -a, --all                 Show connections, files, and modules");
        Console.WriteLine("  -u, --unique              Remove duplicate rows");
        Console.WriteLine("  -j, --json                Output JSON");
        Console.WriteLine("  -h, --help, /?            Show this help");
        Console.WriteLine("                            Help exits immediately; later options are ignored");
        Console.WriteLine();
        Console.WriteLine("Default (no type flags): --network --files");
        Console.WriteLine();
        Console.WriteLine("Exit codes: 0 complete, 1 invalid arguments, 2 incomplete collection, 130 cancelled");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  lsof -i -P 443");
        Console.WriteLine("  lsof -f -p chrome");
        Console.WriteLine("  lsof -a -p 1234 --json");
    }
}
