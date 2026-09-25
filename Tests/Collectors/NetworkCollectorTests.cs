using System.Buffers.Binary;
using System.Globalization;
using Lsof.Collectors;
using Lsof.Models;
using Lsof.Native;
using Lsof.Processes;
using Lsof.Tests.Support;

namespace Lsof.Tests.Collectors;

[Collection("Native and console tests")]
public sealed class NetworkCollectorTests
{
    private const int FixtureProcessId = 1337;
    private const int MaximumTableBufferSize = 128 * 1024 * 1024;

    [Fact]
    public void Collect_decodes_tcp_and_udp_rows_for_ipv4_and_ipv6()
    {
        FakeIpHelperApi nativeApi = new();
        nativeApi.SetTable(true, IpHelperApi.AfInet, CreateTcp4Table());
        nativeApi.SetTable(true, IpHelperApi.AfInet6, CreateTcp6Table());
        nativeApi.SetTable(false, IpHelperApi.AfInet, CreateUdp4Table());
        nativeApi.SetTable(false, IpHelperApi.AfInet6, CreateUdp6Table());

        using ProcessCatalog catalog = TestCatalogFactory.Create(new ProcessInfo(FixtureProcessId, "fixture-process", "C:\\fixture.exe"));
        using TrackingNativeBufferAllocator memory = new();
        List<LsofEntry> entries = new();
        CollectionReport report = new();
        ProcessSelection selection = new ProcessFilter([FixtureProcessId.ToString(CultureInfo.InvariantCulture)]).ResolveSelection(catalog, report, TestContext.Current.CancellationToken);

        NetworkCollector.Collect(catalog, selection, [], entries, nativeApi, memory, report, TestContext.Current.CancellationToken);

        Assert.Equal(4, entries.Count);

        LsofEntry tcp4 = Assert.Single(entries, entry => entry.Protocol == "TCP" && entry.Category == "IPv4");
        Assert.Equal("Established", tcp4.State);
        Assert.Equal("192.0.2.10", tcp4.LocalAddress);
        Assert.Equal(4660, tcp4.LocalPort);
        Assert.Equal("198.51.100.20", tcp4.RemoteAddress);
        Assert.Equal(443, tcp4.RemotePort);
        Assert.Equal(FixtureProcessId, tcp4.ProcessId);

        LsofEntry tcp6 = Assert.Single(entries, entry => entry.Protocol == "TCP" && entry.Category == "IPv6");
        Assert.Equal("Established", tcp6.State);
        Assert.Equal("fe80::1234%9", tcp6.LocalAddress);
        Assert.Equal(62000, tcp6.LocalPort);
        Assert.Equal("2001:db8::2", tcp6.RemoteAddress);
        Assert.Equal(443, tcp6.RemotePort);
        Assert.Equal(FixtureProcessId, tcp6.ProcessId);

        LsofEntry udp4 = Assert.Single(entries, entry => entry.Protocol == "UDP" && entry.Category == "IPv4");
        Assert.Equal("203.0.113.53", udp4.LocalAddress);
        Assert.Equal(5353, udp4.LocalPort);
        Assert.Equal(FixtureProcessId, udp4.ProcessId);

        LsofEntry udp6 = Assert.Single(entries, entry => entry.Protocol == "UDP" && entry.Category == "IPv6");
        Assert.Equal("fe80::abcd%17", udp6.LocalAddress);
        Assert.Equal(5354, udp6.LocalPort);
        Assert.Equal(FixtureProcessId, udp6.ProcessId);

        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
        Assert.False(report.HasWarnings);
    }

    [Fact]
    public void Collect_matches_tcp_remote_ports_but_not_an_absent_remote_port_zero()
    {
        FakeIpHelperApi nativeApi = new();
        nativeApi.SetTable(true, IpHelperApi.AfInet, CreateTcp4Table());
        nativeApi.SetTable(true, IpHelperApi.AfInet6, CreateTcp6Table());
        using ProcessCatalog catalog = TestCatalogFactory.Create();
        using TrackingNativeBufferAllocator memory = new();
        List<LsofEntry> entries = new();
        CollectionReport report = new();

        NetworkCollector.Collect(catalog, ProcessSelection.AllProcesses, [443], entries, nativeApi, memory, report, TestContext.Current.CancellationToken);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Equal(443, entry.RemotePort));

        FakeIpHelperApi listeningApi = new();
        listeningApi.SetTable(true, IpHelperApi.AfInet, CreateTcp4Table(remotePort: 0, state: 2));
        entries.Clear();
        report = new CollectionReport();

        NetworkCollector.Collect(catalog, ProcessSelection.AllProcesses, [0], entries, listeningApi, memory, report, TestContext.Current.CancellationToken);

        Assert.Empty(entries);
        Assert.False(report.HasWarnings);
    }

    [Fact]
    public void Collect_treats_no_data_as_an_empty_table_without_warning_or_allocation()
    {
        FakeIpHelperApi nativeApi = new();
        using ProcessCatalog catalog = TestCatalogFactory.Create();
        using TrackingNativeBufferAllocator memory = new();
        List<LsofEntry> entries = new();
        CollectionReport report = new();

        NetworkCollector.Collect(
            catalog,
            ProcessSelection.AllProcesses,
            [],
            entries,
            nativeApi,
            memory,
            report,
            TestContext.Current.CancellationToken);

        Assert.Empty(entries);
        Assert.Empty(report.Diagnostics);
        Assert.Empty(memory.AllocationSizes);
        Assert.Equal(0, memory.OutstandingCount);
    }

    [Fact]
    public void Collect_rejects_a_truncated_native_table_and_frees_its_buffer()
    {
        FakeIpHelperApi nativeApi = new();
        byte[] tableWithZeroRows = new byte[sizeof(int)];
        WriteInt32(tableWithZeroRows, 0, 0);
        nativeApi.SetTable(true, IpHelperApi.AfInet, tableWithZeroRows, reportedSize: sizeof(int) - 1);

        using ProcessCatalog catalog = TestCatalogFactory.Create();
        using TrackingNativeBufferAllocator memory = new();
        List<LsofEntry> entries = new();
        CollectionReport report = new();
        NetworkCollector.Collect(catalog, ProcessSelection.AllProcesses, [], entries, nativeApi, memory, report, TestContext.Current.CancellationToken);

        Assert.Empty(entries);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Level == CollectionDiagnosticLevel.Warning && diagnostic.Message.Contains("truncated table", StringComparison.Ordinal));
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Collect_rejects_invalid_row_counts_and_frees_its_buffer(int rowCount)
    {
        FakeIpHelperApi nativeApi = new();
        byte[] headerOnlyTable = new byte[sizeof(int)];
        WriteInt32(headerOnlyTable, 0, rowCount);
        nativeApi.SetTable(true, IpHelperApi.AfInet, headerOnlyTable);

        using ProcessCatalog catalog = TestCatalogFactory.Create();
        using TrackingNativeBufferAllocator memory = new();
        List<LsofEntry> entries = new();
        CollectionReport report = new();
        NetworkCollector.Collect(catalog, ProcessSelection.AllProcesses, [], entries, nativeApi, memory, report, TestContext.Current.CancellationToken);

        Assert.Empty(entries);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Level == CollectionDiagnosticLevel.Warning && diagnostic.Message.Contains("invalid table size", StringComparison.Ordinal));
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
    }

    [Fact]
    public void QueryTable_bounds_retries_when_the_native_table_keeps_growing()
    {
        FakeIpHelperApi nativeApi = new();
        nativeApi.SetHandler(true, IpHelperApi.AfInet, (IntPtr table, ref int size) =>
        {
            size = table == IntPtr.Zero ? 16 : size + 1;
            return IpHelperApi.ErrorInsufficientBuffer;
        });

        using ProcessCatalog catalog = TestCatalogFactory.Create();
        using TrackingNativeBufferAllocator memory = new();
        List<LsofEntry> entries = new();
        CollectionReport report = new();
        NetworkCollector.Collect(catalog, ProcessSelection.AllProcesses, [], entries, nativeApi, memory, report, TestContext.Current.CancellationToken);

        IpHelperCall[] calls = nativeApi.Calls.Where(call => call.IsTcp && call.Family == IpHelperApi.AfInet).ToArray();
        Assert.Equal(9, calls.Length); // One sizing query plus the eight bounded allocation attempts.
        Assert.Single(calls, call => call.Buffer == IntPtr.Zero);
        Assert.NotEmpty(memory.AllocationSizes);
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.All(memory.AllocationSizes, size => Assert.InRange(size, 1, MaximumTableBufferSize));
        Assert.Equal(0, memory.OutstandingCount);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Level == CollectionDiagnosticLevel.Warning && diagnostic.Message.Contains("retry limit", StringComparison.Ordinal));
        Assert.Empty(entries);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void QueryTable_rejects_oversized_tables_without_exceeding_the_allocation_cap(bool oversizedOnSizingQuery)
    {
        FakeIpHelperApi nativeApi = new();
        nativeApi.SetHandler(true, IpHelperApi.AfInet, (IntPtr table, ref int size) =>
        {
            size = table == IntPtr.Zero && !oversizedOnSizingQuery
                ? 16
                : MaximumTableBufferSize + 1;
            return IpHelperApi.ErrorInsufficientBuffer;
        });

        using ProcessCatalog catalog = TestCatalogFactory.Create();
        using TrackingNativeBufferAllocator memory = new();
        List<LsofEntry> entries = new();
        CollectionReport report = new();
        NetworkCollector.Collect(catalog, ProcessSelection.AllProcesses, [], entries, nativeApi, memory, report, TestContext.Current.CancellationToken);

        if (oversizedOnSizingQuery)
        {
            Assert.Empty(memory.AllocationSizes);
        }
        else
        {
            Assert.NotEmpty(memory.AllocationSizes);
        }

        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.All(memory.AllocationSizes, size => Assert.InRange(size, 1, MaximumTableBufferSize));
        Assert.Equal(0, memory.OutstandingCount);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Level == CollectionDiagnosticLevel.Warning && diagnostic.Message.Contains("exceeded the buffer size limit", StringComparison.Ordinal));
        Assert.Empty(entries);
    }

    [Fact]
    public void QueryTable_frees_allocated_memory_when_the_native_call_fails()
    {
        FakeIpHelperApi nativeApi = new();
        nativeApi.SetHandler(true, IpHelperApi.AfInet, (IntPtr table, ref int size) =>
        {
            if (table == IntPtr.Zero)
            {
                size = 32;
                return IpHelperApi.ErrorInsufficientBuffer;
            }

            return 5;
        });

        using ProcessCatalog catalog = TestCatalogFactory.Create();
        using TrackingNativeBufferAllocator memory = new();
        List<LsofEntry> entries = new();
        CollectionReport report = new();
        NetworkCollector.Collect(catalog, ProcessSelection.AllProcesses, [], entries, nativeApi, memory, report, TestContext.Current.CancellationToken);

        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Level == CollectionDiagnosticLevel.Warning && diagnostic.Message.Contains("Windows error 5", StringComparison.Ordinal));
        Assert.NotEmpty(memory.AllocationSizes);
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
        Assert.Empty(entries);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void QueryTable_rejects_success_sizes_larger_than_the_allocated_buffer_and_frees_it(int reportedSize)
    {
        FakeIpHelperApi nativeApi = new();
        nativeApi.SetHandler(true, IpHelperApi.AfInet, (IntPtr table, ref int size) =>
        {
            if (table == IntPtr.Zero)
            {
                size = sizeof(int);
                return IpHelperApi.ErrorInsufficientBuffer;
            }

            size = reportedSize;
            return IpHelperApi.ErrorSuccess;
        });

        using ProcessCatalog catalog = TestCatalogFactory.Create();
        using TrackingNativeBufferAllocator memory = new();
        List<LsofEntry> entries = new();
        CollectionReport report = new();
        NetworkCollector.Collect(catalog, ProcessSelection.AllProcesses, [], entries, nativeApi, memory, report, TestContext.Current.CancellationToken);

        Assert.Contains(report.Diagnostics, diagnostic =>
            diagnostic.Level == CollectionDiagnosticLevel.Warning
            && diagnostic.Message.Contains("invalid table size", StringComparison.Ordinal));
        Assert.Equal(memory.AllocationSizes.Count, memory.FreedCount);
        Assert.Equal(0, memory.OutstandingCount);
        Assert.Empty(entries);
    }

    private static byte[] CreateTcp4Table(int remotePort = 443, int state = 5)
    {
        byte[] table = CreateTable(rowSize: 24);
        int row = sizeof(int);
        WriteInt32(table, row, state);
        WriteBytes(table, row + 4, [192, 0, 2, 10]);
        WriteNetworkPort(table, row + 8, 4660);
        WriteBytes(table, row + 12, [198, 51, 100, 20]);
        WriteNetworkPort(table, row + 16, remotePort);
        WriteInt32(table, row + 20, FixtureProcessId);
        return table;
    }

    private static byte[] CreateTcp6Table()
    {
        byte[] table = CreateTable(rowSize: 56);
        int row = sizeof(int);
        WriteBytes(table, row, [0xfe, 0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x12, 0x34]);
        WriteInt32(table, row + 16, 9);
        WriteNetworkPort(table, row + 20, 62000);
        WriteBytes(table, row + 24, [0x20, 0x01, 0x0d, 0xb8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2]);
        WriteInt32(table, row + 40, 0);
        WriteNetworkPort(table, row + 44, 443);
        WriteInt32(table, row + 48, 5); // MIB_TCP_STATE_ESTAB.
        WriteInt32(table, row + 52, FixtureProcessId);
        return table;
    }

    private static byte[] CreateUdp4Table()
    {
        byte[] table = CreateTable(rowSize: 12);
        int row = sizeof(int);
        WriteBytes(table, row, [203, 0, 113, 53]);
        WriteNetworkPort(table, row + 4, 5353);
        WriteInt32(table, row + 8, FixtureProcessId);
        return table;
    }

    private static byte[] CreateUdp6Table()
    {
        byte[] table = CreateTable(rowSize: 28);
        int row = sizeof(int);
        WriteBytes(table, row, [0xfe, 0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xab, 0xcd]);
        WriteInt32(table, row + 16, 17);
        WriteNetworkPort(table, row + 20, 5354);
        WriteInt32(table, row + 24, FixtureProcessId);
        return table;
    }

    private static byte[] CreateTable(int rowSize)
    {
        byte[] table = new byte[sizeof(int) + rowSize];
        WriteInt32(table, 0, 1);
        return table;
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), value);
    }

    private static void WriteNetworkPort(byte[] buffer, int offset, int port)
    {
        ushort networkOrderPort = BinaryPrimitives.ReverseEndianness((ushort)port);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), networkOrderPort);
    }

    private static void WriteBytes(byte[] buffer, int offset, byte[] bytes)
    {
        bytes.CopyTo(buffer, offset);
    }

}
