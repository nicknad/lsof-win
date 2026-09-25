using System.Security.Principal;
using Lsof.Models;
using Lsof.Native;
using Lsof.Processes;

namespace Lsof.Collectors;

internal static class FileHandleCollector
{
    public static void Collect(
        ProcessCatalog catalog,
        ProcessSelection selection,
        List<LsofEntry> entries,
        CollectionReport report,
        Action<WindowsHandleApi.HandleScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Production wiring: WindowsHandleApi enumerates the native handle table and IsElevated
        // checks the current token. Both are parameters so tests can inject fakes.
        Collect(catalog, selection, entries, report, progress, WindowsHandleApi.EnumerateDiskFileHandles, IsElevated, cancellationToken);
    }

    internal static void Collect(
        ProcessCatalog catalog,
        ProcessSelection selection,
        List<LsofEntry> entries,
        CollectionReport report,
        Action<WindowsHandleApi.HandleScanProgress>? progress,
        WindowsHandleApi.HandleEnumerator enumerateHandles,
        Func<bool> isElevated,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (selection.IsEmpty)
        {
            return;
        }

        if (!isElevated())
        {
            report.AddWarning("Warning: not running elevated; handles owned by other users will be skipped.");
        }
        WindowsHandleApi.EnumerationResult enumeration = enumerateHandles(selection, progress, cancellationToken);
        foreach (string warning in enumeration.Warnings)
        {
            report.AddWarning(warning);
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (WindowsHandleApi.OpenFileHandle handle in enumeration.Handles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LsofEntry entry = catalog.CreateEntry(handle.ProcessId);
            entry.Kind = LsofEntryKind.File;
            entry.Handle = "0x" + handle.Handle.ToString("x");
            entry.Name = handle.Path;
            entries.Add(entry);
        }
    }

    // Uses the .NET Windows principal APIs: GetCurrent reads this process's login token and
    // IsInRole(Administrator) asks the OS whether it belongs to the local Administrators group.
    private static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
