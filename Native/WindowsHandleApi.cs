using System.Runtime.InteropServices;
using Lsof.Processes;

namespace Lsof.Native;

internal static class WindowsHandleApi
{
    private const int SystemExtendedHandleInformation = 64; // SYSTEM_INFORMATION_CLASS value.
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004); // STATUS_INFO_LENGTH_MISMATCH.
    private const int StatusSuccess = 0; // STATUS_SUCCESS.
    private const int ProcessDuplicateHandle = 0x0040; // PROCESS_DUP_HANDLE access right.
    private const uint DuplicateSameAccess = 0x00000002; // DUPLICATE_SAME_ACCESS option.
    private const uint FileTypeDisk = 0x0001; // FILE_TYPE_DISK result from GetFileType.
    private const int InitialHandleBufferSize = 1 << 20; // Start at 1 MiB; resize if the kernel reports more.
    private const int MaximumHandleBufferSize = 256 * 1024 * 1024; // Bound the native table allocation to 256 MiB.
    private const int MaximumHandleQueryAttempts = 8; // Bound retries while the live handle table changes.
    private const int HandleBufferGrowth = 1 << 16; // Add 64 KiB beyond the reported requirement.
    private const int MaximumFinalPathLength = 32768; // Cap buffers near Windows' extended-path limit.
    private const int InitialFinalPathBufferSize = 512; // Retry with the required size if the path is longer.
    private const int ExtendedUncPrefixLength = 8; // Length of the "\\?\UNC\" prefix.
    private const int ExtendedPathPrefixLength = 4; // Length of the "\\?\" prefix.

    // SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX layout returned by NtQuerySystemInformation.
    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemHandleEntry
    {
        public IntPtr Object;
        public IntPtr UniqueProcessId;
        public IntPtr HandleValue;
        public uint GrantedAccess;
        public ushort CreatorBackTraceIndex;
        public ushort ObjectTypeIndex;
        public uint HandleAttributes;
        public uint Reserved;
    }

    public sealed class OpenFileHandle
    {
        public int ProcessId { get; init; }
        public long Handle { get; init; }
        public string Path { get; init; } = "";
    }

    internal readonly record struct HandleScanProgress(long ProcessedHandles, long TotalHandles, int FilesFound);

    internal sealed record EnumerationResult(OpenFileHandle[] Handles, string[] Warnings);

    internal delegate EnumerationResult HandleEnumerator(
        ProcessSelection selection,
        Action<HandleScanProgress>? progress,
        CancellationToken cancellationToken);
    private static readonly IWindowsHandleNativeApi NativeApi = new WindowsHandleNativeApi();

    public static EnumerationResult EnumerateDiskFileHandles(
        ProcessSelection selection,
        Action<HandleScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return EnumerateDiskFileHandles(selection, NativeApi, HGlobalMemoryAllocator.Instance, progress, cancellationToken);
    }

    internal static EnumerationResult EnumerateDiskFileHandles(
        ProcessSelection selection,
        IWindowsHandleNativeApi nativeApi,
        INativeMemoryAllocator memoryAllocator,
        Action<HandleScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (selection.IsEmpty)
        {
            return new EnumerationResult(Array.Empty<OpenFileHandle>(), Array.Empty<string>());
        }

        List<OpenFileHandle> results = new();
        List<string> warnings = new();
        Dictionary<int, IntPtr> processHandlesById = new();
        IntPtr buffer = QuerySystemHandleTable(nativeApi, memoryAllocator, out int bufferSize, warnings, cancellationToken);
        if (buffer == IntPtr.Zero)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new EnumerationResult(results.ToArray(), warnings.ToArray());
        }

        OpenFileHandle[] handles;
        long skippedHandleCount = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetHandleEntries(buffer, bufferSize, out long count, out int entrySize, out IntPtr entryPointer, out string? warning))
            {
                IntPtr currentProcess = nativeApi.GetCurrentProcess(); // Pseudo-handle of this process; target for duplicated handles.

                for (long i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Marshal one native SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX row into managed form, then advance the cursor.
                    SystemHandleEntry entry = Marshal.PtrToStructure<SystemHandleEntry>(entryPointer);
                    entryPointer = IntPtr.Add(entryPointer, entrySize);

                    int processId = unchecked((int)entry.UniqueProcessId.ToInt64());
                    if (selection.Includes(processId))
                    {
                        IntPtr processHandle = GetProcessHandle(nativeApi, processHandlesById, processId);
                        if (processHandle == IntPtr.Zero)
                        {
                            skippedHandleCount++;
                        }
                        else
                        {
                            OpenFileHandle? fileHandle = TryCreateDiskFileHandle(
                                processHandle,
                                currentProcess,
                                entry,
                                processId,
                                nativeApi,
                                warnings,
                                out bool inspectionFailed);
                            if (inspectionFailed)
                            {
                                skippedHandleCount++;
                            }

                            if (fileHandle is not null)
                            {
                                results.Add(fileHandle);
                            }
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    long processedHandles = i + 1;
                    if (processedHandles % 256 == 0 || processedHandles == count)
                    {
                        progress?.Invoke(new HandleScanProgress(processedHandles, count, results.Count));
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (count == 0)
                {
                    progress?.Invoke(new HandleScanProgress(0, 0, 0));
                }

                if (skippedHandleCount > 0)
                {
                    warnings.Add($"Warning: could not inspect {skippedHandleCount} matching system handle(s).");
                }

                handles = results.ToArray();
            }
            else
            {
                warnings.Add(warning!);
                handles = Array.Empty<OpenFileHandle>();
            }
        }
        finally
        {
            foreach (IntPtr processHandle in processHandlesById.Values)
            {
                CloseHandleIfOpen(nativeApi, processHandle, warnings);
            }

            if (buffer != IntPtr.Zero)
            {
                memoryAllocator.Free(buffer);
            }
        }

        return new EnumerationResult(handles, warnings.ToArray());
    }

    private static IntPtr QuerySystemHandleTable(
        IWindowsHandleNativeApi nativeApi,
        INativeMemoryAllocator memoryAllocator,
        out int bufferSize,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        bufferSize = InitialHandleBufferSize;
        IntPtr buffer = IntPtr.Zero;
        int status = StatusInfoLengthMismatch;
        int requiredSize = 0;

        try
        {
            for (int attempt = 0; attempt < MaximumHandleQueryAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                buffer = memoryAllocator.Allocate(bufferSize);
                // Ask the kernel to fill the buffer with the system-wide handle table; requiredSize receives the bytes needed.
                status = nativeApi.QuerySystemInformation(SystemExtendedHandleInformation, buffer, bufferSize, out requiredSize);
                cancellationToken.ThrowIfCancellationRequested();
                if (status != StatusInfoLengthMismatch)
                {
                    break;
                }

                memoryAllocator.Free(buffer);
                buffer = IntPtr.Zero;

                int nextBufferSize = NativeBufferSizing.GetNextSize(
                    bufferSize,
                    requiredSize,
                    MaximumHandleBufferSize,
                    HandleBufferGrowth);
                if (nextBufferSize == 0)
                {
                    break;
                }

                bufferSize = nextBufferSize;
            }
        }
        catch
        {
            if (buffer != IntPtr.Zero)
            {
                memoryAllocator.Free(buffer);
            }

            throw;
        }

        if (status == StatusSuccess)
        {
            if (requiredSize < 0 || requiredSize > bufferSize)
            {
                warnings.Add("Warning: the system returned an invalid handle-table size.");
                memoryAllocator.Free(buffer);
                return IntPtr.Zero;
            }

            bufferSize = requiredSize;
        }

        if (status == StatusInfoLengthMismatch)
        {
            warnings.Add("Warning: the system handle table exceeded the query retry or memory limit.");
        }
        else if (status != StatusSuccess)
        {
            warnings.Add($"Warning: could not query system handles (NTSTATUS 0x{status:X8}).");
        }

        if (status != StatusSuccess && buffer != IntPtr.Zero)
        {
            memoryAllocator.Free(buffer);
            buffer = IntPtr.Zero;
        }

        return buffer;
    }

    private static bool TryGetHandleEntries(
        IntPtr buffer,
        int bufferSize,
        out long count,
        out int entrySize,
        out IntPtr entryPointer,
        out string? warning)
    {
        // Marshal.SizeOf gives the expected native row size used to validate the returned table.
        return TryGetHandleEntries(buffer, bufferSize, IntPtr.Size, Marshal.SizeOf<SystemHandleEntry>(), out count, out entrySize, out entryPointer, out warning);
    }

    internal static bool TryGetHandleEntries(
        IntPtr buffer,
        int bufferSize,
        int pointerSize,
        int rowSize,
        out long count,
        out int entrySize,
        out IntPtr entryPointer,
        out string? warning)
    {
        long headerSize = pointerSize * 2L; // ULONG_PTR handle count followed by ULONG_PTR reserved.
        entrySize = rowSize;
        count = 0;
        entryPointer = IntPtr.Zero;
        warning = null;

        if (pointerSize is not (sizeof(int) or sizeof(long)) || rowSize <= 0)
        {
            warning = "Warning: the system returned an invalid handle-table layout.";
            return false;
        }

        if (bufferSize < headerSize)
        {
            warning = "Warning: the system returned a truncated handle table.";
            return false;
        }

        // Marshal.ReadInt64/ReadInt32 reads the ULONG_PTR NumberOfHandles field that heads the table.
        count = pointerSize == sizeof(long) ? Marshal.ReadInt64(buffer) : Marshal.ReadInt32(buffer); // NumberOfHandles is ULONG_PTR.
        long maximumEntryCount = (bufferSize - headerSize) / entrySize;
        if (count < 0 || count > maximumEntryCount)
        {
            warning = "Warning: the system returned an invalid handle-table size.";
            return false;
        }

        entryPointer = IntPtr.Add(buffer, (int)headerSize);
        return true;
    }

    private static IntPtr GetProcessHandle(IWindowsHandleNativeApi nativeApi, Dictionary<int, IntPtr> processHandlesById, int processId)
    {
        if (!processHandlesById.TryGetValue(processId, out IntPtr processHandle))
        {
            // OpenProcess asks the kernel for a handle to the owning process; PROCESS_DUP_HANDLE
            // is the access right that lets us copy handles out of it.
            processHandle = nativeApi.OpenProcess(ProcessDuplicateHandle, false, processId);
            processHandlesById.Add(processId, processHandle);
        }

        return processHandle;
    }

    private static OpenFileHandle? TryCreateDiskFileHandle(
        IntPtr processHandle,
        IntPtr currentProcess,
        SystemHandleEntry entry,
        int processId,
        IWindowsHandleNativeApi nativeApi,
        List<string> warnings,
        out bool inspectionFailed)
    {
        inspectionFailed = false;
        // DuplicateHandle copies the target process's handle into this process so the object can be queried.
        // Desired access is ignored when DUPLICATE_SAME_ACCESS is set.
        if (!nativeApi.DuplicateHandle(processHandle, entry.HandleValue, currentProcess, out IntPtr duplicate, 0, false, DuplicateSameAccess))
        {
            inspectionFailed = true;
            return null;
        }

        try
        {
            // GetFileType tells us what kind of object the handle references; only disk files are in scope.
            uint fileType = nativeApi.GetFileType(duplicate);
            if (fileType != FileTypeDisk)
            {
                // Non-disk and unknown handle types are outside this collector's scope.
                return null;
            }

            string? path = GetPathFromHandle(nativeApi, duplicate);
            if (path is null)
            {
                inspectionFailed = true;
                return null;
            }

            return new OpenFileHandle
            {
                ProcessId = processId,
                Handle = entry.HandleValue.ToInt64(),
                Path = path
            };
        }
        finally
        {
            CloseHandleIfOpen(nativeApi, duplicate, warnings);
        }
    }

    // Resolves a handle to its final DOS path, retrying once with the size reported by the API.
    internal static string? GetPathFromHandle(IWindowsHandleNativeApi nativeApi, IntPtr handle)
    {
        char[] buffer = new char[InitialFinalPathBufferSize];
        // GetFinalPathNameByHandle fills the buffer and returns the required character count.
        uint length = nativeApi.GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Length, 0); // Zero flags request a normalized DOS path.
        if (length == 0 || length > MaximumFinalPathLength)
        {
            return null;
        }
        if (length >= buffer.Length)
        {
            buffer = new char[(int)length + 1]; // Reserve one extra character for the terminator.
            // Second call retries with the length the first call reported.
            length = nativeApi.GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Length, 0);
            if (length == 0 || length > MaximumFinalPathLength || length >= buffer.Length)
            {
                return null;
            }
        }

        string path = new(buffer, 0, (int)length);
        if (path.StartsWith("\\\\?\\UNC\\", StringComparison.Ordinal))
        {
            path = string.Concat("\\\\", path.AsSpan(ExtendedUncPrefixLength));
        }
        else if (path.StartsWith("\\\\?\\", StringComparison.Ordinal))
        {
            path = new string(path.AsSpan(ExtendedPathPrefixLength));
        }
        return path;
    }

    private static void CloseHandleIfOpen(IWindowsHandleNativeApi nativeApi, IntPtr handle, List<string> warnings)
    {
        // CloseHandle releases the kernel handle; a failure is only reported as a warning.
        if (handle != IntPtr.Zero && !nativeApi.CloseHandle(handle))
        {
            warnings.Add("Warning: failed to close a native handle.");
        }
    }
}
