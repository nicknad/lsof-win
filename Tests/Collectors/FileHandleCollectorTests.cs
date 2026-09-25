using Lsof.Collectors;
using Lsof.Models;
using Lsof.Native;
using Lsof.Processes;
using Lsof.Tests.Support;

namespace Lsof.Tests.Collectors;

public sealed class FileHandleCollectorTests
{
    private const int FixtureProcessId = 31415;

    [Fact]
    public void Collect_empty_selection_skips_elevation_check_and_handle_enumeration()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create();
        CollectionReport report = new();
        List<LsofEntry> entries = new();
        bool elevationChecked = false;
        bool enumerationCalled = false;

        FileHandleCollector.Collect(
            catalog,
            ProcessSelection.FromProcessIds([]),
            entries,
            report,
            progress: null,
            enumerateHandles: (_, _, _) =>
            {
                enumerationCalled = true;
                return new WindowsHandleApi.EnumerationResult([], []);
            },
            isElevated: () =>
            {
                elevationChecked = true;
                return false;
            },
            TestContext.Current.CancellationToken);

        Assert.Empty(entries);
        Assert.Empty(report.Diagnostics);
        Assert.False(elevationChecked);
        Assert.False(enumerationCalled);
    }

    [Fact]
    public void Collect_reports_elevation_warning_when_not_elevated()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create();
        CollectionReport report = new();
        List<LsofEntry> entries = new();
        WindowsHandleApi.HandleEnumerator emptyEnumeration = (_, _, _) =>
            new WindowsHandleApi.EnumerationResult([], []);

        FileHandleCollector.Collect(
            catalog,
            ProcessSelection.FromProcessIds([FixtureProcessId]),
            entries,
            report,
            progress: null,
            enumerateHandles: emptyEnumeration,
            isElevated: () => false,
            TestContext.Current.CancellationToken);

        Assert.Empty(entries);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Message.Contains("not running elevated", StringComparison.Ordinal));
    }

    [Fact]
    public void Collect_formats_native_handles_as_hex_and_keeps_the_resolved_path()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create(new ProcessInfo(FixtureProcessId, "fixture", "C:\\fixture.exe"));
        CollectionReport report = new();
        List<LsofEntry> entries = new();
        WindowsHandleApi.HandleEnumerator enumerate = (selection, _, _) =>
        {
            Assert.True(selection.Includes(FixtureProcessId));
            return new WindowsHandleApi.EnumerationResult(
                [new WindowsHandleApi.OpenFileHandle
                {
                    ProcessId = FixtureProcessId,
                    Handle = 0xABC,
                    Path = "C:\\temp\\fixture.txt"
                }],
                []);
        };

        FileHandleCollector.Collect(
            catalog,
            ProcessSelection.FromProcessIds([FixtureProcessId]),
            entries,
            report,
            progress: null,
            enumerateHandles: enumerate,
            isElevated: () => true,
            TestContext.Current.CancellationToken);

        LsofEntry entry = Assert.Single(entries);
        Assert.Equal(LsofEntryKind.File, entry.Kind);
        Assert.Equal("0xabc", entry.Handle);
        Assert.Equal("C:\\temp\\fixture.txt", entry.Name);
        Assert.Empty(report.Diagnostics);
    }
}
