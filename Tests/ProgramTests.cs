using Lsof.Cli;
using Lsof.Models;

namespace Lsof.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void GetExitCode_is_zero_for_complete_collection_and_information_messages()
    {
        CollectionReport report = new();
        report.AddInformation("No matching process.");

        Assert.Equal(0, Program.GetExitCode(report));
    }

    [Fact]
    public void GetExitCode_is_nonzero_when_collection_is_incomplete()
    {
        CollectionReport report = new();
        report.AddWarning("A requested table could not be read.");

        Assert.Equal(2, Program.GetExitCode(report));
    }

    [Fact]
    public void Run_returns_the_cancellation_exit_code_when_cancelled_during_collection()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Equal(130, Program.Run(["--network"], cancellation.Token));
    }

    [Fact]
    public void ApplyDefaultCollectors_selects_network_and_files_when_no_type_flag_is_present()
    {
        Assert.True(CommandLineOptions.TryParse([], out CommandLineOptions options, out _));

        Program.ApplyDefaultCollectors(options);

        Assert.True(options.Network);
        Assert.True(options.Files);
        Assert.False(options.Modules);
    }

    [Fact]
    public void ApplyDefaultCollectors_preserves_an_explicit_type_selection()
    {
        Assert.True(CommandLineOptions.TryParse(["--modules"], out CommandLineOptions options, out _));

        Program.ApplyDefaultCollectors(options);

        Assert.False(options.Network);
        Assert.False(options.Files);
        Assert.True(options.Modules);
    }

    [Fact]
    public void Run_returns_one_for_unknown_arguments()
    {
        Assert.Equal(1, Program.Run(["--bogus"], TestContext.Current.CancellationToken));
    }
}
