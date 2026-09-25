using System.ComponentModel;
using System.Diagnostics;
using Lsof.Models;
using Lsof.Tests.Support;
using Lsof.Processes;

namespace Lsof.Tests.Processes;

public sealed class ProcessCatalogTests
{
    [Fact]
    public void IsExpectedInspectionException_does_not_swallow_general_invalid_operation_errors()
    {
        Assert.False(ProcessCatalog.IsExpectedInspectionException(new InvalidOperationException("unexpected logic error")));
    }

    [Fact]
    public void IsExpectedProcessInspectionException_does_not_treat_a_disposed_process_as_exit_race()
    {
        Process process = Process.GetCurrentProcess();
        process.Dispose();

        Assert.False(ProcessCatalog.IsExpectedProcessInspectionException(process, new InvalidOperationException("unexpected logic error")));
    }

    [Theory]
    [InlineData(typeof(Win32Exception))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(NotSupportedException))]
    public void IsExpectedInspectionException_recognizes_process_inspection_failures(Type exceptionType)
    {
        Exception exception = (Exception)Activator.CreateInstance(exceptionType)!;

        Assert.True(ProcessCatalog.IsExpectedInspectionException(exception));
    }

    [Fact]
    public void CreateEntry_uses_system_and_pid_fallbacks()
    {
        using ProcessCatalog catalog = TestCatalogFactory.Create(new ProcessInfo(123, "", "C:\\hidden.exe"));

        LsofEntry idle = catalog.CreateEntry(0);
        Assert.Equal("Idle", idle.ProcessName);
        Assert.Equal(0, idle.ProcessId);

        LsofEntry system = catalog.CreateEntry(4);
        Assert.Equal("System", system.ProcessName);
        Assert.Equal(4, system.ProcessId);

        LsofEntry unnamed = catalog.CreateEntry(123);
        Assert.Equal("PID 123", unnamed.ProcessName);
        Assert.Equal("C:\\hidden.exe", unnamed.ProcessPath);

        LsofEntry unknown = catalog.CreateEntry(456);
        Assert.Equal("PID 456", unknown.ProcessName);
        Assert.Equal("", unknown.ProcessPath);
    }
}
