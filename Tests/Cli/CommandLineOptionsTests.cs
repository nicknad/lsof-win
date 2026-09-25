using System.Globalization;
using Lsof.Cli;

namespace Lsof.Tests.Cli;

public sealed class CommandLineOptionsTests
{
    [Fact]
    public void TryParse_accepts_empty_argument_list()
    {
        bool parsed = CommandLineOptions.TryParse([], out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Empty(options.ProcessFilters);
        Assert.Empty(options.PortFilters);
    }

    [Theory]
    [InlineData("-p")]
    [InlineData("--process")]
    public void TryParse_accepts_separate_process_aliases(string option)
    {
        bool parsed = CommandLineOptions.TryParse([option, "alpha"], out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Equal("alpha", Assert.Single(options.ProcessFilters));
    }

    [Fact]
    public void TryParse_accepts_inline_process_form()
    {
        bool parsed = CommandLineOptions.TryParse(["--process=gamma"], out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Equal("gamma", Assert.Single(options.ProcessFilters));
    }

    [Theory]
    [InlineData("-P")]
    [InlineData("--port")]
    public void TryParse_accepts_separate_port_aliases(string option)
    {
        bool parsed = CommandLineOptions.TryParse([option, "443"], out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Equal(443, Assert.Single(options.PortFilters));
    }

    [Fact]
    public void TryParse_accepts_inline_port_form()
    {
        bool parsed = CommandLineOptions.TryParse(["--port=8080"], out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Equal(8080, Assert.Single(options.PortFilters));
    }

    [Theory]
    [InlineData("-i", "network")]
    [InlineData("--network", "network")]
    [InlineData("-f", "files")]
    [InlineData("--files", "files")]
    [InlineData("-m", "modules")]
    [InlineData("--modules", "modules")]
    [InlineData("-a", "all")]
    [InlineData("--all", "all")]
    [InlineData("-u", "unique")]
    [InlineData("--unique", "unique")]
    [InlineData("-j", "json")]
    [InlineData("--json", "json")]
    public void TryParse_sets_each_switch_alias(string option, string expectedFlag)
    {
        bool parsed = CommandLineOptions.TryParse([option], out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Equal(expectedFlag is "network" or "all", options.Network);
        Assert.Equal(expectedFlag is "files" or "all", options.Files);
        Assert.Equal(expectedFlag is "modules" or "all", options.Modules);
        Assert.Equal(expectedFlag == "unique", options.Unique);
        Assert.Equal(expectedFlag == "json", options.Json);
    }

    [Fact]
    public void TryParse_splits_comma_separated_values_trims_whitespace_and_skips_empty_segments()
    {
        string[] args =
        [
            "--process=  alpha , , beta, ",
            "--port", " 80 , , 443 , "
        ];

        bool parsed = CommandLineOptions.TryParse(args, out CommandLineOptions options, out _);

        Assert.True(parsed);
        Assert.Equal(["alpha", "beta"], options.ProcessFilters);
        Assert.Equal([80, 443], options.PortFilters);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65535)]
    public void TryParse_accepts_port_bounds(int port)
    {
        bool parsed = CommandLineOptions.TryParse(["--port", port.ToString(CultureInfo.InvariantCulture)], out CommandLineOptions options, out _);

        Assert.True(parsed);
        Assert.Equal([port], options.PortFilters);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("2147483648")]
    [InlineData("not-a-port")]
    public void TryParse_rejects_invalid_port_values(string value)
    {
        bool parsed = CommandLineOptions.TryParse(["--port", value], out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_rejects_negative_port_bounds_in_inline_form()
    {
        bool parsed = CommandLineOptions.TryParse(["--port=-1"], out _, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--process=")]
    [InlineData("--process", "")]
    [InlineData("--process", " , ")]
    [InlineData("--port=")]
    [InlineData("--port", "")]
    [InlineData("--port", " , ")]
    public void TryParse_rejects_empty_arguments_or_filter_values(params string[] args)
    {
        bool parsed = CommandLineOptions.TryParse(args, out _, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("-p")]
    [InlineData("--process")]
    [InlineData("-P")]
    [InlineData("--port")]
    public void TryParse_rejects_missing_filter_values(string option)
    {
        bool parsed = CommandLineOptions.TryParse([option], out _, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("-p", "--json")]
    [InlineData("--process", "-j")]
    [InlineData("-P", "--json")]
    [InlineData("--port", "/?")]
    public void TryParse_rejects_option_tokens_as_separate_filter_values(string option, string value)
    {
        bool parsed = CommandLineOptions.TryParse([option, value], out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_accepts_option_looking_process_values_in_inline_form()
    {
        string[] args = ["--process=--json"];
        bool parsed = CommandLineOptions.TryParse(args, out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.False(options.Json);
        Assert.Equal("--json", Assert.Single(options.ProcessFilters));
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("/?")]
    public void TryParse_accepts_help_aliases(string option)
    {
        bool parsed = CommandLineOptions.TryParse([option], out _, out bool showHelp);

        Assert.True(parsed);
        Assert.True(showHelp);
    }

    [Fact]
    public void TryParse_help_short_circuits_later_arguments()
    {
        bool parsed = CommandLineOptions.TryParse(["-i", "--help", "--bogus"], out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.True(showHelp);
        Assert.True(options.Network);
    }

    [Fact]
    public void TryParse_rejects_unknown_tokens()
    {
        bool parsed = CommandLineOptions.TryParse(["--bogus"], out _, out _);

        Assert.False(parsed);
    }
}
