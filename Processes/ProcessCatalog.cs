using System.ComponentModel;
using System.Diagnostics;
using Lsof.Models;

namespace Lsof.Processes;

internal sealed class ProcessCatalog : IDisposable
{
    private const int IdleProcessId = 0; // Windows reserves PID 0 for the Idle process.
    private const int SystemProcessId = 4; // Windows reserves PID 4 for the System process.

    private readonly Dictionary<int, ProcessInfo> _processInfoById;

    internal ProcessCatalog(Process[] processes, Dictionary<int, ProcessInfo> processInfoById)
    {
        Processes = processes;
        _processInfoById = processInfoById;
    }

    public Process[] Processes { get; }

    public IEnumerable<ProcessInfo> ProcessInfos => _processInfoById.Values;

    public static ProcessCatalog Create(CollectionReport report, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception exception) when (IsExpectedInspectionException(exception))
        {
            report.AddWarning($"Warning: could not enumerate processes: {exception.Message}");
            processes = Array.Empty<Process>();
        }

        Dictionary<int, ProcessInfo> processInfoById = new();
        int processIdInspectionFailures = 0;
        int processNameInspectionFailures = 0;
        int processPathInspectionFailures = 0;
        try
        {
            foreach (Process process in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int id;
                try
                {
                    id = process.Id;
                }
                catch (Exception exception) when (IsExpectedProcessInspectionException(process, exception))
                {
                    processIdInspectionFailures++;
                    continue;
                }

                if (processInfoById.ContainsKey(id))
                {
                    continue;
                }

                string name = "";
                try
                {
                    name = process.ProcessName;
                }
                catch (Exception exception) when (IsExpectedProcessInspectionException(process, exception))
                {
                    processNameInspectionFailures++;
                }

                string path = "";
                try
                {
                    path = process.MainModule?.FileName ?? "";
                }
                catch (Exception exception) when (IsExpectedProcessInspectionException(process, exception))
                {
                    processPathInspectionFailures++;
                }

                processInfoById.Add(id, new ProcessInfo(id, name, path));
            }
        }
        catch
        {
            DisposeProcesses(processes);
            throw;
        }

        if (processIdInspectionFailures + processNameInspectionFailures + processPathInspectionFailures > 0)
        {
            report.AddWarning(
                $"Warning: process metadata could not be read for {processIdInspectionFailures + processNameInspectionFailures + processPathInspectionFailures} inspection(s) " +
                $"(IDs: {processIdInspectionFailures}, names: {processNameInspectionFailures}, paths: {processPathInspectionFailures}).");
        }

        return new ProcessCatalog(processes, processInfoById);
    }

    public LsofEntry CreateEntry(int processId)
    {
        if (_processInfoById.TryGetValue(processId, out ProcessInfo? info))
        {
            return new LsofEntry
            {
                ProcessId = processId,
                ProcessName = string.IsNullOrEmpty(info.Name) ? GetFallbackName(processId) : info.Name,
                ProcessPath = info.Path
            };
        }

        return new LsofEntry { ProcessId = processId, ProcessName = GetFallbackName(processId) };
    }

    public void Dispose()
    {
        DisposeProcesses(Processes);
    }

    internal static bool IsExpectedInspectionException(Exception exception)
    {
        return exception is Win32Exception
            or UnauthorizedAccessException
            or NotSupportedException;
    }

    internal static bool IsExpectedProcessInspectionException(Process process, Exception exception)
    {
        if (exception is not InvalidOperationException)
        {
            return IsExpectedInspectionException(exception);
        }

        try
        {
            return process.HasExited;
        }
        catch (Exception hasExitedException) when (hasExitedException is Win32Exception or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string GetFallbackName(int processId)
    {
        return processId switch
        {
            IdleProcessId => "Idle",
            SystemProcessId => "System",
            _ => $"PID {processId}"
        };
    }

    private static void DisposeProcesses(IEnumerable<Process> processes)
    {
        foreach (Process process in processes)
        {
            process.Dispose();
        }
    }
}
