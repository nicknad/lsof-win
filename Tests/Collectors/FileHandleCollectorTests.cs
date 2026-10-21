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
        List<LsofEntry> entries = [];
        bool elevationChecked = false;
        bool enumerationCalled = false;

        FileHandleCollector.Collect(
            catalog,
            ProcessSelection.FromProcessIds([]),
            FileFilter.Empty,
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
            TestContext.Current.CancellationToken
        );

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
        List<LsofEntry> entries = [];
        static WindowsHandleApi.EnumerationResult EmptyEnumeration(
            ProcessSelection _,
            Action<WindowsHandleApi.HandleScanProgress>? _1,
            CancellationToken _2
        )
        {
            return new([], []);
        }

        FileHandleCollector.Collect(
            catalog,
            ProcessSelection.FromProcessIds([FixtureProcessId]),
            FileFilter.Empty,
            entries,
            report,
            progress: null,
            enumerateHandles: EmptyEnumeration,
            isElevated: () => false,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(entries);
        Assert.Contains(
            report.Diagnostics,
            diagnostic =>
                diagnostic.Message.Contains("not running elevated", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Collect_formats_native_handles_as_hex_and_keeps_the_resolved_path()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create(
            new ProcessInfo(FixtureProcessId, "fixture", "C:\\fixture.exe")
        );
        CollectionReport report = new();
        List<LsofEntry> entries = [];
        static WindowsHandleApi.EnumerationResult Enumerate(
            ProcessSelection selection,
            Action<WindowsHandleApi.HandleScanProgress>? _,
            CancellationToken _1
        )
        {
            Assert.True(selection.Includes(FixtureProcessId));
            return new WindowsHandleApi.EnumerationResult(
                [
                    new WindowsHandleApi.OpenFileHandle
                    {
                        ProcessId = FixtureProcessId,
                        Handle = 0xABC,
                        Path = "C:\\temp\\fixture.txt",
                    },
                ],
                []
            );
        }

        FileHandleCollector.Collect(
            catalog,
            ProcessSelection.FromProcessIds([FixtureProcessId]),
            FileFilter.Empty,
            entries,
            report,
            progress: null,
            enumerateHandles: Enumerate,
            isElevated: () => true,
            TestContext.Current.CancellationToken
        );

        LsofEntry entry = Assert.Single(entries);
        Assert.Equal(LsofEntryKind.File, entry.Kind);
        Assert.Equal("0xabc", entry.Handle);
        Assert.Equal("C:\\temp\\fixture.txt", entry.Name);
        Assert.Empty(report.Diagnostics);
    }

    [Fact]
    public void Collect_keeps_only_handles_matching_the_file_filter()
    {
        const string wanted = "C:\\temp\\wanted.txt";
        using ProcessCatalog catalog = TestCatalogFactory.Create(
            new ProcessInfo(FixtureProcessId, "fixture", "C:\\fixture.exe")
        );
        CollectionReport report = new();
        List<LsofEntry> entries = [];
        static WindowsHandleApi.EnumerationResult EnumerateWanted(
            ProcessSelection _,
            Action<WindowsHandleApi.HandleScanProgress>? _1,
            CancellationToken _2
        )
        {
            return new(
                [
                    new WindowsHandleApi.OpenFileHandle
                    {
                        ProcessId = FixtureProcessId,
                        Handle = 0x1,
                        Path = wanted,
                    },
                    new WindowsHandleApi.OpenFileHandle
                    {
                        ProcessId = FixtureProcessId,
                        Handle = 0x2,
                        Path = "C:\\temp\\other.txt",
                    },
                ],
                []
            );
        }

        FileHandleCollector.Collect(
            catalog,
            ProcessSelection.FromProcessIds([FixtureProcessId]),
            new FileFilter([wanted]),
            entries,
            report,
            progress: null,
            enumerateHandles: EnumerateWanted,
            isElevated: () => true,
            TestContext.Current.CancellationToken
        );

        LsofEntry entry = Assert.Single(entries);
        Assert.Equal(wanted, entry.Name);
    }
}
