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
    [InlineData("-c")]
    [InlineData("--command")]
    [InlineData("-u")]
    [InlineData("--user")]
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

    [Theory]
    [InlineData("-c")]
    [InlineData("--command")]
    public void TryParse_accepts_command_alias_as_a_process_filter(string option)
    {
        bool parsed = CommandLineOptions.TryParse([option, "bash"], out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Equal("bash", Assert.Single(options.ProcessFilters));
    }

    [Fact]
    public void TryParse_accepts_inline_command_form()
    {
        bool parsed = CommandLineOptions.TryParse(["--command=chrome"], out CommandLineOptions options, out _);

        Assert.True(parsed);
        Assert.Equal("chrome", Assert.Single(options.ProcessFilters));
    }

    [Theory]
    [InlineData("-u")]
    [InlineData("--user")]
    public void TryParse_accepts_separate_user_aliases(string option)
    {
        bool parsed = CommandLineOptions.TryParse([option, "alice"], out CommandLineOptions options, out bool showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Equal("alice", Assert.Single(options.UserFilters));
        Assert.False(options.Unique);
    }

    [Fact]
    public void TryParse_accepts_inline_user_form_and_splits_commas()
    {
        bool parsed = CommandLineOptions.TryParse(["--user= DOMAIN\\alice , bob "], out CommandLineOptions options, out _);

        Assert.True(parsed);
        Assert.Equal(["DOMAIN\\alice", "bob"], options.UserFilters);
    }

    [Fact]
    public void TryParse_distinguishes_unique_flag_from_user_filter()
    {
        bool uniqueParsed = CommandLineOptions.TryParse(["--unique"], out CommandLineOptions uniqueOptions, out _);
        bool userParsed = CommandLineOptions.TryParse(["-u", "alice"], out CommandLineOptions userOptions, out _);

        Assert.True(uniqueParsed);
        Assert.True(uniqueOptions.Unique);
        Assert.Empty(uniqueOptions.UserFilters);
        Assert.True(userParsed);
        Assert.False(userOptions.Unique);
        Assert.Equal(["alice"], userOptions.UserFilters);
    }

    [Theory]
    [InlineData("-i:22", 22)]
    [InlineData("-i:443,80", 443)]
    public void TryParse_accepts_attached_network_port_spec(string option, int expectedPort)
    {
        bool parsed = CommandLineOptions.TryParse([option], out CommandLineOptions options, out _);

        Assert.True(parsed);
        Assert.True(options.Network);
        Assert.Contains(expectedPort, options.PortFilters);
    }

    [Theory]
    [InlineData(":22", 22)]
    [InlineData("tcp:443", 443)]
    [InlineData("80,443", 80)]
    public void TryParse_accepts_separate_network_port_spec(string spec, int expectedPort)
    {
        bool parsed = CommandLineOptions.TryParse(["-i", spec], out CommandLineOptions options, out _);

        Assert.True(parsed);
        Assert.True(options.Network);
        Assert.Contains(expectedPort, options.PortFilters);
    }

    [Fact]
    public void TryParse_treats_network_switch_as_a_flag_when_the_next_token_is_an_option()
    {
        bool parsed = CommandLineOptions.TryParse(["-i", "--files"], out CommandLineOptions options, out _);

        Assert.True(parsed);
        Assert.True(options.Network);
        Assert.True(options.Files);
        Assert.Empty(options.PortFilters);
    }

    [Theory]
    [InlineData("C:\\Windows\\notepad.exe")]
    [InlineData("/pfad/zur/datei")]
    public void TryParse_treats_bare_tokens_as_path_filters(string path)
    {
        bool parsed = CommandLineOptions.TryParse([path], out CommandLineOptions options, out _);

        Assert.True(parsed);
        Assert.Equal(path, Assert.Single(options.PathFilters));
    }

    [Fact]
    public void TryParse_accepts_a_path_filter_combined_with_type_flags()
    {
        bool parsed = CommandLineOptions.TryParse(["-f", "C:\\temp\\a.txt"], out CommandLineOptions options, out _);

        Assert.True(parsed);
        Assert.True(options.Files);
        Assert.Equal("C:\\temp\\a.txt", Assert.Single(options.PathFilters));
    }
}
