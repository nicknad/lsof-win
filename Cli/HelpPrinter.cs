namespace Lsof.Cli;

internal static class HelpPrinter
{
    public static void Print()
    {
        foreach (string line in CliReference.Lines)
        {
            Console.WriteLine(line);
        }
    }
}
