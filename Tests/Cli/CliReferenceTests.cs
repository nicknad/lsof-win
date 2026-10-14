using Lsof.Cli;

namespace Lsof.Tests.Cli;

public sealed class CliReferenceTests
{
    [Fact]
    public void HelpPrinter_prints_the_canonical_reference_verbatim()
    {
        TextWriter original = Console.Out;
        using StringWriter writer = new();
        Console.SetOut(writer);
        try
        {
            HelpPrinter.Print();
        }
        finally
        {
            Console.SetOut(original);
        }

        string expected = string.Join(Environment.NewLine, CliReference.Lines) + Environment.NewLine;
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void Readme_contains_the_canonical_options_block()
    {
        string readmePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../README.md"));
        Assert.True(File.Exists(readmePath), $"README not found at {readmePath}");
        string readme = File.ReadAllText(readmePath);

        foreach (string line in CliReference.Lines.Where(static l =>
            l.StartsWith("  -", StringComparison.Ordinal) ||
            l.StartsWith("      ", StringComparison.Ordinal) ||
            l.StartsWith("  <", StringComparison.Ordinal) ||
            l.StartsWith("                            ", StringComparison.Ordinal)))
        {
            Assert.Contains(line, readme, StringComparison.Ordinal);
        }

        Assert.Contains("Cli/CliReference.cs", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("-u, --unique", readme, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-p", "chrome")]
    [InlineData("--process", "chrome")]
    [InlineData("-c", "chrome")]
    [InlineData("-u", "alice")]
    [InlineData("-P", "443")]
    [InlineData("-i", ":22")]
    public void Parser_accepts_every_documented_two_token_form(string option, string value)
    {
        bool parsed = CommandLineOptions.TryParse([option, value], out _, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
    }

    [Theory]
    [InlineData("--process=chrome")]
    [InlineData("--command=chrome")]
    [InlineData("--user=alice")]
    [InlineData("--port=443")]
    [InlineData("-i")]
    [InlineData("--network")]
    [InlineData("-i:22")]
    [InlineData("-f")]
    [InlineData("--files")]
    [InlineData("-m")]
    [InlineData("--modules")]
    [InlineData("-a")]
    [InlineData("--all")]
    [InlineData("--unique")]
    [InlineData("-j")]
    [InlineData("--json")]
    public void Parser_accepts_every_documented_single_token_form(string arg)
    {
        bool parsed = CommandLineOptions.TryParse([arg], out _, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
    }
}
