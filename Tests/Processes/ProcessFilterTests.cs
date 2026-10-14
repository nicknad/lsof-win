using System.Globalization;
using Lsof.Models;
using Lsof.Processes;
using Lsof.Tests.Support;

namespace Lsof.Tests.Processes;

[Collection("Native and console tests")]
public sealed class ProcessFilterTests
{
    [Theory]
    [InlineData("chrome", "CHROME")]
    public void Matches_process_names_case_insensitively(string pattern, string name)
    {
        ProcessFilter filter = new([pattern]);

        Assert.True(filter.Matches(42, name));
    }

    [Fact]
    public void Matches_empty_filter_list_matches_every_process()
    {
        ProcessFilter filter = new([]);

        Assert.True(filter.Matches(42, "anything"));
        Assert.True(filter.Matches(42, ""));
    }

    [Fact]
    public void Matches_multiple_name_filters_use_any_of_semantics_and_ignore_empty_names()
    {
        ProcessFilter filter = new(["notepad", "chrome"]);

        Assert.True(filter.Matches(42, "chrome.exe"));
        Assert.False(filter.Matches(42, "unknown"));
        Assert.False(filter.Matches(42, ""));
    }

    [Fact]
    public void Matches_supports_star_and_single_character_wildcards()
    {
        ProcessFilter starFilter = new(["chr*me"]);
        ProcessFilter questionFilter = new(["c?r?me"]);

        Assert.True(starFilter.Matches(42, "chrome"));
        Assert.True(questionFilter.Matches(42, "chrome"));
        Assert.False(questionFilter.Matches(42, "chromium"));
    }

    [Theory]
    [InlineData("notepad", "NOTEPAD.EXE", true)]
    [InlineData("notepad.exe", "NOTEPAD", true)]
    [InlineData("notepad", "notepad.txt", false)]
    public void Matches_normalizes_the_exe_suffix(string pattern, string name, bool expected)
    {
        ProcessFilter filter = new([pattern]);

        Assert.Equal(expected, filter.Matches(42, name));
    }

    [Fact]
    public void Matches_and_resolves_numeric_process_ids()
    {
        const int processId = 12345;
        ProcessFilter filter = new([processId.ToString(CultureInfo.InvariantCulture)]);
        using ProcessCatalog catalog = TestCatalogFactory.Create(new ProcessInfo(processId, "app", "C:\\app.exe"));
        CollectionReport report = new();
        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        Assert.True(filter.Matches(processId, "different-name"));
        Assert.False(filter.Matches(processId + 1, processId.ToString(CultureInfo.InvariantCulture)));
        Assert.False(selection.IncludesAllProcesses);
        Assert.Contains(processId, selection.ProcessIds);
        Assert.False(report.HasWarnings);
    }

    [Fact]
    public void ResolveSelection_returns_empty_and_reports_when_no_name_matches()
    {
        ProcessFilter filter = new(["missing-process-name"]);
        using ProcessCatalog catalog = TestCatalogFactory.Create();
        CollectionReport report = new();

        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        Assert.True(selection.IsEmpty);
        CollectionDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(CollectionDiagnosticLevel.Information, diagnostic.Level);
        Assert.Contains("No running process matches: missing-process-name", diagnostic.Message);
    }

    [Fact]
    public void ResolveSelection_excludes_a_numeric_pid_missing_from_the_process_snapshot()
    {
        const int processId = 987654;
        ProcessFilter filter = new([processId.ToString(CultureInfo.InvariantCulture)]);
        using ProcessCatalog catalog = TestCatalogFactory.Create();
        CollectionReport report = new();
        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        Assert.True(selection.IsEmpty);
        Assert.Contains("No running process matches", Assert.Single(report.Diagnostics).Message);
        Assert.False(report.HasWarnings);
    }

    [Fact]
    public void ResolveSelection_returns_all_processes_when_no_filter_is_supplied()
    {
        ProcessFilter filter = new([]);
        using ProcessCatalog catalog = TestCatalogFactory.Create();
        CollectionReport report = new();

        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        Assert.True(selection.IncludesAllProcesses);
        Assert.False(selection.IsEmpty);
        Assert.Empty(selection.ProcessIds);
        Assert.Empty(report.Diagnostics);
    }

    [Fact]
    public void ResolveSelection_matches_processes_by_owner_account_name()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create(
            new ProcessInfo(1, "one", "C:\\one.exe"),
            new ProcessInfo(2, "two", "C:\\two.exe"));
        CollectionReport report = new();
        FakeOwnerProvider owners = new(new Dictionary<int, string>
        {
            [1] = "DOMAIN\\alice",
            [2] = "DOMAIN\\bob"
        });
        ProcessFilter filter = new([], ["alice"], owners);

        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        Assert.False(selection.IncludesAllProcesses);
        Assert.Contains(1, selection.ProcessIds);
        Assert.DoesNotContain(2, selection.ProcessIds);
        Assert.False(report.HasWarnings);
    }

    [Fact]
    public void ResolveSelection_matches_domain_qualified_user_filters()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create(new ProcessInfo(1, "one", "C:\\one.exe"));
        CollectionReport report = new();
        FakeOwnerProvider owners = new(new Dictionary<int, string> { [1] = "DOMAIN\\alice" });
        ProcessFilter filter = new([], ["domain\\ALICE"], owners);

        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        Assert.Contains(1, selection.ProcessIds);
    }

    [Fact]
    public void ResolveSelection_ors_process_name_and_user_filters()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create(
            new ProcessInfo(1, "one", "C:\\one.exe"),
            new ProcessInfo(2, "two", "C:\\two.exe"),
            new ProcessInfo(3, "three", "C:\\three.exe"));
        CollectionReport report = new();
        FakeOwnerProvider owners = new(new Dictionary<int, string>
        {
            [1] = "DOMAIN\\bob",
            [2] = "DOMAIN\\alice",
            [3] = "DOMAIN\\carol"
        });
        ProcessFilter filter = new(["one"], ["alice"], owners);

        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        Assert.Contains(1, selection.ProcessIds);
        Assert.Contains(2, selection.ProcessIds);
        Assert.DoesNotContain(3, selection.ProcessIds);
    }

    [Fact]
    public void ResolveSelection_reports_information_when_no_user_matches()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create(new ProcessInfo(1, "one", "C:\\one.exe"));
        CollectionReport report = new();
        FakeOwnerProvider owners = new(new Dictionary<int, string> { [1] = "DOMAIN\\alice" });
        ProcessFilter filter = new([], ["missing-user"], owners);

        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        Assert.True(selection.IsEmpty);
        Assert.Contains("No running process matches: missing-user", Assert.Single(report.Diagnostics).Message);
    }

    [Fact]
    public void ResolveSelection_skips_processes_whose_owner_cannot_be_resolved()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create(new ProcessInfo(1, "one", "C:\\one.exe"));
        CollectionReport report = new();
        FakeOwnerProvider owners = new(new Dictionary<int, string>());
        ProcessFilter filter = new([], ["alice"], owners);

        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        Assert.True(selection.IsEmpty);
    }

    private sealed class FakeOwnerProvider : IProcessOwnerProvider
    {
        private readonly Dictionary<int, string> _owners;

        public FakeOwnerProvider(Dictionary<int, string> owners)
        {
            _owners = owners;
        }

        public string? GetOwner(int processId)
        {
            return _owners.TryGetValue(processId, out string? owner) ? owner : null;
        }
    }
}
