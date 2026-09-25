using System.ComponentModel;
using System.Diagnostics;
using Lsof.Collectors;
using Lsof.Models;
using Lsof.Processes;
using Lsof.Tests.Support;

namespace Lsof.Tests.Collectors;

[Collection("Native and console tests")]
public sealed class ModuleCollectorTests
{
    [Fact]
    public void Collect_emits_paths_from_the_module_snapshot()
    {
        Process process = Process.GetCurrentProcess();
        using ProcessCatalog catalog = TestCatalogFactory.CreateWithProcess(process);
        ProcessSelection selection = ProcessSelection.FromProcessIds([process.Id]);
        FakeModuleProvider provider = new(new ModuleSnapshot(["C:\\one.dll", "C:\\two.dll"], 0));
        CollectionReport report = new();
        List<LsofEntry> entries = new();

        ModuleCollector.Collect(catalog, selection, entries, report, provider, TestContext.Current.CancellationToken);

        Assert.Collection(
            entries,
            entry => Assert.Equal("C:\\one.dll", entry.Name),
            entry => Assert.Equal("C:\\two.dll", entry.Name));
        Assert.All(entries, entry => Assert.Equal(LsofEntryKind.Module, entry.Kind));
        Assert.Empty(report.Diagnostics);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void Collect_reports_a_module_provider_failure_as_incomplete_data()
    {
        Process process = Process.GetCurrentProcess();
        using ProcessCatalog catalog = TestCatalogFactory.CreateWithProcess(process);
        ProcessSelection selection = ProcessSelection.FromProcessIds([process.Id]);
        FakeModuleProvider provider = new(new Win32Exception(5));
        CollectionReport report = new();
        List<LsofEntry> entries = new();

        ModuleCollector.Collect(catalog, selection, entries, report, provider, TestContext.Current.CancellationToken);

        Assert.Empty(entries);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Message.Contains("module lists", StringComparison.Ordinal));
    }

    [Fact]
    public void SnapshotModules_materializes_the_ProcessModuleCollection()
    {
        using Process process = Process.GetCurrentProcess();
        ProcessModuleCollection modules = process.Modules;

        ProcessModule[] snapshot = ProcessModuleProvider.SnapshotModules(modules, TestContext.Current.CancellationToken);

        Assert.Equal(modules.Count, snapshot.Length);
        for (int index = 0; index < modules.Count; index++)
        {
            Assert.Same(modules[index], snapshot[index]);
        }
    }

    private sealed class FakeModuleProvider : IProcessModuleProvider
    {
        private readonly ModuleSnapshot? _snapshot;
        private readonly Exception? _exception;

        public FakeModuleProvider(ModuleSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public FakeModuleProvider(Exception exception)
        {
            _exception = exception;
        }

        public int CallCount { get; private set; }

        public ModuleSnapshot GetModules(Process process, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (_exception is not null)
            {
                throw _exception;
            }

            return _snapshot!;
        }
    }
}
