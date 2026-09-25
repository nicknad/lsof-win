using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Lsof.Native;
using Lsof.Processes;
using Lsof.Tests.Support;

namespace Lsof.Tests.Native;

[Collection("Native and console tests")]
public sealed class WindowsHandleApiTests
{
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int StatusSuccess = 0;
    private const int MaximumHandleBufferSize = 256 * 1024 * 1024;
    private const int HandleBufferGrowth = 1 << 16;
    private const int FixtureProcessId = 43210;
    private const long FixtureHandle = 0x1234;

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    public void TryGetHandleEntries_reads_pointer_sized_counts_and_offsets(int pointerSize)
    {
        const int rowSize = 16;
        int headerSize = pointerSize * 2;
        int bufferSize = headerSize + rowSize;
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            WritePointerSized(buffer, 1, pointerSize);
            WritePointerSized(IntPtr.Add(buffer, pointerSize), 0, pointerSize);

            bool valid = WindowsHandleApi.TryGetHandleEntries(
                buffer,
                bufferSize,
                pointerSize,
                rowSize,
                out long count,
                out int actualRowSize,
                out IntPtr entryPointer,
                out string? warning);

            Assert.True(valid);
            Assert.Equal(1, count);
            Assert.Equal(rowSize, actualRowSize);
            Assert.Equal(IntPtr.Add(buffer, headerSize), entryPointer);
            Assert.Null(warning);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    public void TryGetHandleEntries_rejects_truncated_headers_for_each_pointer_size(int pointerSize)
    {
        int headerSize = pointerSize * 2;
        IntPtr buffer = Marshal.AllocHGlobal(headerSize);
        try
        {
            bool valid = WindowsHandleApi.TryGetHandleEntries(
                buffer,
                headerSize - 1,
                pointerSize,
                16,
                out _,
                out _,
                out _,
                out string? warning);

            Assert.False(valid);
            Assert.Contains("truncated handle table", warning);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Theory]
    [InlineData(4, -1)]
    [InlineData(4, 2)]
    [InlineData(8, -1)]
    [InlineData(8, 2)]
    public void TryGetHandleEntries_rejects_negative_and_oversized_row_counts(int pointerSize, int rowCount)
    {
        const int rowSize = 16;
        int headerSize = pointerSize * 2;
        int bufferSize = headerSize + rowSize;
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            WritePointerSized(buffer, rowCount, pointerSize);

            bool valid = WindowsHandleApi.TryGetHandleEntries(
                buffer,
                bufferSize,
                pointerSize,
                rowSize,
                out _,
                out _,
                out _,
                out string? warning);

            Assert.False(valid);
            Assert.Contains("invalid handle-table size", warning);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public void EnumerateDiskFileHandles_decodes_the_fixed_system_handle_row_layout()
    {
        byte[] table = CreateSingleHandleTable(FixtureProcessId, FixtureHandle);
        FakeWindowsHandleNativeApi nativeApi = new()
        {
            SystemInformationHandler = (int _, IntPtr buffer, int _, out int returnLength) =>
            {
                Marshal.Copy(table, 0, buffer, table.Length);
                returnLength = table.Length;
                return StatusSuccess;
            }
        };

        using TrackingNativeBufferAllocator memory = new();
        WindowsHandleApi.EnumerationResult result = WindowsHandleApi.EnumerateDiskFileHandles(
            ProcessSelection.FromProcessIds([FixtureProcessId]),
            nativeApi,
            memory,
            cancellationToken: TestContext.Current.CancellationToken);

        WindowsHandleApi.OpenFileHandle handle = Assert.Single(result.Handles);
        Assert.Equal(FixtureProcessId, handle.ProcessId);
        Assert.Equal(FixtureHandle, handle.Handle);
        Assert.Equal("C:\\fixture.txt", handle.Path);
        Assert.Equal(new[] { FixtureProcessId }, nativeApi.OpenedProcessIds);
        Assert.Equal(2, nativeApi.ClosedHandles.Count);
        Assert.Equal(Marshal.SizeOf<WindowsHandleApi.SystemHandleEntry>(), IntPtr.Size == 8 ? 40 : 28);
        Assert.Empty(result.Warnings);
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
    }

    [Fact]
    public void EnumerateDiskFileHandles_empty_selection_skips_native_queries_and_allocations()
    {
        FakeWindowsHandleNativeApi nativeApi = new();
        using TrackingNativeBufferAllocator memory = new();

        WindowsHandleApi.EnumerationResult result = WindowsHandleApi.EnumerateDiskFileHandles(
            ProcessSelection.FromProcessIds([]),
            nativeApi,
            memory,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Handles);
        Assert.Empty(result.Warnings);
        Assert.Empty(nativeApi.SystemInformationCalls);
        Assert.Empty(memory.AllocationSizes);
    }

    [Fact]
    public void QuerySystemHandleTable_bounds_retries_and_frees_every_allocation()
    {
        FakeWindowsHandleNativeApi nativeApi = new()
        {
            SystemInformationHandler = (int _, IntPtr _, int bufferSize, out int returnLength) =>
            {
                returnLength = bufferSize + 1;
                return StatusInfoLengthMismatch;
            }
        };
        using TrackingNativeBufferAllocator memory = new();

        WindowsHandleApi.EnumerationResult result = WindowsHandleApi.EnumerateDiskFileHandles(
            ProcessSelection.AllProcesses,
            nativeApi,
            memory,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(8, nativeApi.SystemInformationCalls.Count);
        Assert.NotEmpty(memory.AllocationSizes);
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.All(memory.AllocationSizes, size => Assert.InRange(size, 1, MaximumHandleBufferSize));
        Assert.Equal(0, memory.OutstandingCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("retry or memory limit", StringComparison.Ordinal));
    }

    [Fact]
    public void QuerySystemHandleTable_rejects_reported_sizes_over_the_cap_and_frees_the_buffer()
    {
        FakeWindowsHandleNativeApi nativeApi = new()
        {
            SystemInformationHandler = (int _, IntPtr _, int _, out int returnLength) =>
            {
                returnLength = MaximumHandleBufferSize + 1;
                return StatusInfoLengthMismatch;
            }
        };
        using TrackingNativeBufferAllocator memory = new();

        WindowsHandleApi.EnumerationResult result = WindowsHandleApi.EnumerateDiskFileHandles(
            ProcessSelection.AllProcesses,
            nativeApi,
            memory,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(nativeApi.SystemInformationCalls);
        Assert.All(memory.AllocationSizes, size => Assert.InRange(size, 1, MaximumHandleBufferSize));
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("retry or memory limit", StringComparison.Ordinal));
    }

    [Fact]
    public void QuerySystemHandleTable_rejects_a_success_length_larger_than_the_allocation()
    {
        FakeWindowsHandleNativeApi nativeApi = new()
        {
            SystemInformationHandler = (int _, IntPtr _, int bufferSize, out int returnLength) =>
            {
                returnLength = bufferSize + 1;
                return StatusSuccess;
            }
        };
        using TrackingNativeBufferAllocator memory = new();

        WindowsHandleApi.EnumerationResult result = WindowsHandleApi.EnumerateDiskFileHandles(
            ProcessSelection.AllProcesses,
            nativeApi,
            memory,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(result.Warnings, warning => warning.Contains("invalid handle-table size", StringComparison.Ordinal));
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
    }

    [Fact]
    public void QuerySystemHandleTable_reports_native_failure_and_frees_the_buffer()
    {
        FakeWindowsHandleNativeApi nativeApi = new()
        {
            SystemInformationHandler = (int _, IntPtr _, int _, out int returnLength) =>
            {
                returnLength = 0;
                return unchecked((int)0xC0000001);
            }
        };
        using TrackingNativeBufferAllocator memory = new();

        WindowsHandleApi.EnumerationResult result = WindowsHandleApi.EnumerateDiskFileHandles(
            ProcessSelection.AllProcesses,
            nativeApi,
            memory,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Handles);
        Assert.Contains(result.Warnings, warning => warning.Contains("could not query system handles", StringComparison.Ordinal));
        Assert.NotEmpty(memory.AllocationSizes);
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
    }

    [Theory]
    [InlineData("open-process", 0)]
    [InlineData("duplicate", 1)]
    [InlineData("path", 2)]
    public void EnumerateDiskFileHandles_reports_partial_inspection_and_closes_owned_handles(string failure, int expectedClosedHandles)
    {
        FakeWindowsHandleNativeApi nativeApi = CreateNativeApiWithOneFileHandle();
        switch (failure)
        {
            case "open-process":
                nativeApi.OpenProcessSucceeds = false;
                break;
            case "duplicate":
                nativeApi.DuplicateHandleSucceeds = false;
                break;
            case "path":
                nativeApi.FinalPath = null;
                break;
        }

        using TrackingNativeBufferAllocator memory = new();
        WindowsHandleApi.EnumerationResult result = WindowsHandleApi.EnumerateDiskFileHandles(
            ProcessSelection.FromProcessIds([FixtureProcessId]),
            nativeApi,
            memory,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Handles);
        Assert.Contains(result.Warnings, warning => warning.Contains("could not inspect 1 matching system handle", StringComparison.Ordinal));
        Assert.Equal(expectedClosedHandles, nativeApi.ClosedHandles.Count);
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
    }

    [Theory]
    [InlineData(100, 200, MaximumHandleBufferSize, HandleBufferGrowth, 200 + HandleBufferGrowth)]
    [InlineData(100, 50, MaximumHandleBufferSize, HandleBufferGrowth, 200)]
    [InlineData(MaximumHandleBufferSize - 1000, MaximumHandleBufferSize - 500, MaximumHandleBufferSize, HandleBufferGrowth, MaximumHandleBufferSize)]
    [InlineData(100, MaximumHandleBufferSize + 1, MaximumHandleBufferSize, HandleBufferGrowth, 0)]
    [InlineData(MaximumHandleBufferSize, MaximumHandleBufferSize, MaximumHandleBufferSize, HandleBufferGrowth, 0)]
    public void NativeBufferSizing_GetNextSize_applies_growth_and_cap_rules(
        int current,
        int required,
        int maximumSize,
        int growth,
        int expected)
    {
        Assert.Equal(expected, NativeBufferSizing.GetNextSize(current, required, maximumSize, growth));
    }

    [Theory]
    [InlineData(@"\\?\C:\temp\file.txt", @"C:\temp\file.txt")]
    [InlineData(@"\\?\UNC\server\share\file.txt", @"\\server\share\file.txt")]
    public void GetPathFromHandle_strips_extended_drive_and_unc_prefixes(string nativePath, string expectedPath)
    {
        FakeWindowsHandleNativeApi nativeApi = new() { FinalPath = nativePath };

        string? path = WindowsHandleApi.GetPathFromHandle(nativeApi, new IntPtr(17));

        Assert.Equal(expectedPath, path);
    }

    [Fact]
    public void GetPathFromHandle_retries_long_paths_and_rejects_paths_over_the_limit()
    {
        FakeWindowsHandleNativeApi longPathApi = new() { FinalPath = new string('x', 700) };
        string? longPath = WindowsHandleApi.GetPathFromHandle(longPathApi, new IntPtr(17));

        Assert.Equal(700, longPath?.Length);
        Assert.Equal(2, longPathApi.FinalPathCallCount);

        FakeWindowsHandleNativeApi oversizedPathApi = new() { FinalPath = new string('x', 32769) };
        string? oversizedPath = WindowsHandleApi.GetPathFromHandle(oversizedPathApi, new IntPtr(17));

        Assert.Null(oversizedPath);
        Assert.Equal(1, oversizedPathApi.FinalPathCallCount);
    }

    private static byte[] CreateSingleHandleTable(int processId, long handleValue)
    {
        int pointerSize = IntPtr.Size;
        int entrySize = pointerSize == sizeof(long) ? 40 : 28;
        int headerSize = pointerSize * 2;
        byte[] table = new byte[headerSize + entrySize];
        WritePointerSized(table, 0, 1, pointerSize);
        WritePointerSized(table, pointerSize, 0, pointerSize);

        int row = headerSize;
        WritePointerSized(table, row, 0x1111, pointerSize);
        WritePointerSized(table, row + pointerSize, processId, pointerSize);
        WritePointerSized(table, row + 2 * pointerSize, handleValue, pointerSize);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(row + 3 * pointerSize), 0x00120089);
        BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(row + 3 * pointerSize + 4), 0x3344);
        BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(row + 3 * pointerSize + 6), 0x5566);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(row + 3 * pointerSize + 8), 0x778899AA);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(row + 3 * pointerSize + 12), 0xAABBCCDD);
        return table;
    }

    private static FakeWindowsHandleNativeApi CreateNativeApiWithOneFileHandle()
    {
        byte[] table = CreateSingleHandleTable(FixtureProcessId, FixtureHandle);
        return new FakeWindowsHandleNativeApi
        {
            SystemInformationHandler = (int _, IntPtr buffer, int _, out int returnLength) =>
            {
                Marshal.Copy(table, 0, buffer, table.Length);
                returnLength = table.Length;
                return StatusSuccess;
            }
        };
    }

    private static void WritePointerSized(byte[] buffer, int offset, long value, int pointerSize)
    {
        if (pointerSize == sizeof(long))
        {
            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset), value);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), checked((int)value));
        }
    }

    private static void WritePointerSized(IntPtr buffer, long value, int pointerSize)
    {
        if (pointerSize == sizeof(long))
        {
            Marshal.WriteInt64(buffer, value);
        }
        else
        {
            Marshal.WriteInt32(buffer, checked((int)value));
        }
    }
}
