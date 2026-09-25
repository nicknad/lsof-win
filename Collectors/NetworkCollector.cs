using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;
using Lsof.Models;
using Lsof.Native;
using Lsof.Processes;

namespace Lsof.Collectors;

internal static class NetworkCollector
{
    private enum TransportProtocol
    {
        Tcp,
        Udp
    }

    // Numeric values match the MIB_TCP_STATE_* constants returned by IP Helper.
    private enum MibTcpState
    {
        Closed = 1,
        Listen = 2,
        SynSent = 3,
        SynReceived = 4,
        Established = 5,
        FinWait1 = 6,
        FinWait2 = 7,
        CloseWait = 8,
        Closing = 9,
        LastAck = 10,
        TimeWait = 11,
        DeleteTcb = 12
    }

    // Byte offsets from Windows MIB_*_OWNER_PID rows.
    private static class Tcp4Layout
    {
        public const int RowSize = 24; // MIB_TCPROW_OWNER_PID size.
        public const int StateOffset = 0;
        public const int LocalAddressOffset = 4;
        public const int LocalPortOffset = 8;
        public const int RemoteAddressOffset = 12;
        public const int RemotePortOffset = 16;
        public const int ProcessIdOffset = 20;
    }

    private static class Tcp6Layout
    {
        public const int RowSize = 56; // MIB_TCP6ROW_OWNER_PID size.
        public const int LocalAddressOffset = 0;
        public const int LocalScopeIdOffset = 16;
        public const int LocalPortOffset = 20;
        public const int RemoteAddressOffset = 24;
        public const int RemoteScopeIdOffset = 40;
        public const int RemotePortOffset = 44;
        public const int StateOffset = 48;
        public const int ProcessIdOffset = 52;
    }

    private static class Udp4Layout
    {
        public const int RowSize = 12; // MIB_UDPROW_OWNER_PID size.
        public const int LocalAddressOffset = 0;
        public const int LocalPortOffset = 4;
        public const int ProcessIdOffset = 8;
    }

    private static class Udp6Layout
    {
        public const int RowSize = 28; // MIB_UDP6ROW_OWNER_PID size.
        public const int LocalAddressOffset = 0;
        public const int LocalScopeIdOffset = 16;
        public const int LocalPortOffset = 20;
        public const int ProcessIdOffset = 24;
    }

    private readonly record struct NetworkRowLayout(
        int RowSize,
        int LocalAddressOffset,
        int? LocalScopeIdOffset,
        int LocalPortOffset,
        int ProcessIdOffset,
        int? RemoteAddressOffset,
        int? RemoteScopeIdOffset,
        int? RemotePortOffset,
        int? StateOffset,
        NetworkAddressFamily AddressFamily);

    private const int MaximumTableBufferSize = 128 * 1024 * 1024; // Bound each native table allocation to 128 MiB.
    private const int MaximumTableQueryAttempts = 8; // Bound retries while connections change.
    private const int TableBufferGrowth = 1 << 16; // Add 64 KiB beyond the reported size.
    private const int IPv6AddressLength = 16; // IPv6 addresses occupy 128 bits.
    private static readonly IIpHelperApi NativeApi = new WindowsIpHelperApi();
    private static readonly INativeMemoryAllocator NativeMemory = HGlobalMemoryAllocator.Instance;

    public static void Collect(
        ProcessCatalog catalog,
        ProcessSelection selection,
        IReadOnlyList<int> portFilters,
        List<LsofEntry> entries,
        CollectionReport report,
        CancellationToken cancellationToken = default)
    {
        Collect(catalog, selection, portFilters, entries, NativeApi, NativeMemory, report, cancellationToken);
    }

    internal static void Collect(
        ProcessCatalog catalog,
        ProcessSelection selection,
        IReadOnlyList<int> portFilters,
        List<LsofEntry> entries,
        IIpHelperApi nativeApi,
        INativeMemoryAllocator memoryAllocator,
        CollectionReport report,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (selection.IsEmpty)
        {
            return;
        }

        CollectTable(TransportProtocol.Tcp, IpHelperApi.AfInet, catalog, selection, portFilters, entries, nativeApi, memoryAllocator, report, cancellationToken);
        CollectTable(TransportProtocol.Tcp, IpHelperApi.AfInet6, catalog, selection, portFilters, entries, nativeApi, memoryAllocator, report, cancellationToken);
        CollectTable(TransportProtocol.Udp, IpHelperApi.AfInet, catalog, selection, portFilters, entries, nativeApi, memoryAllocator, report, cancellationToken);
        CollectTable(TransportProtocol.Udp, IpHelperApi.AfInet6, catalog, selection, portFilters, entries, nativeApi, memoryAllocator, report, cancellationToken);
    }

    private static void CollectTable(
        TransportProtocol protocol,
        int family,
        ProcessCatalog catalog,
        ProcessSelection selection,
        IReadOnlyList<int> portFilters,
        List<LsofEntry> entries,
        IIpHelperApi nativeApi,
        INativeMemoryAllocator memoryAllocator,
        CollectionReport report,
        CancellationToken cancellationToken)
    {
        NetworkRowLayout layout = GetLayout(protocol, family);
        int tableClass = protocol == TransportProtocol.Tcp
            ? IpHelperApi.TcpTableOwnerPidAll
            : IpHelperApi.UdpTableOwnerPid;
        IntPtr buffer = QueryTable(protocol, family, tableClass, nativeApi, memoryAllocator, report, cancellationToken, out int bufferSize);
        if (buffer == IntPtr.Zero) return;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadRowCount(buffer, bufferSize, layout.RowSize, report, out int count)) return;

            IntPtr entry = IntPtr.Add(buffer, sizeof(int)); // A DWORD row count precedes the entries.

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LsofEntry? row = DecodeRow(protocol, entry, layout, catalog, selection, portFilters);
                if (row is not null)
                {
                    entries.Add(row);
                }

                entry = IntPtr.Add(entry, layout.RowSize);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            memoryAllocator.Free(buffer);
        }
    }

    private static NetworkRowLayout GetLayout(TransportProtocol protocol, int family)
    {
        return (protocol, family) switch
        {
            (TransportProtocol.Tcp, IpHelperApi.AfInet) => new(
                Tcp4Layout.RowSize,
                Tcp4Layout.LocalAddressOffset,
                null,
                Tcp4Layout.LocalPortOffset,
                Tcp4Layout.ProcessIdOffset,
                Tcp4Layout.RemoteAddressOffset,
                null,
                Tcp4Layout.RemotePortOffset,
                Tcp4Layout.StateOffset,
                NetworkAddressFamily.IPv4),
            (TransportProtocol.Tcp, IpHelperApi.AfInet6) => new(
                Tcp6Layout.RowSize,
                Tcp6Layout.LocalAddressOffset,
                Tcp6Layout.LocalScopeIdOffset,
                Tcp6Layout.LocalPortOffset,
                Tcp6Layout.ProcessIdOffset,
                Tcp6Layout.RemoteAddressOffset,
                Tcp6Layout.RemoteScopeIdOffset,
                Tcp6Layout.RemotePortOffset,
                Tcp6Layout.StateOffset,
                NetworkAddressFamily.IPv6),
            (TransportProtocol.Udp, IpHelperApi.AfInet) => new(
                Udp4Layout.RowSize,
                Udp4Layout.LocalAddressOffset,
                null,
                Udp4Layout.LocalPortOffset,
                Udp4Layout.ProcessIdOffset,
                null,
                null,
                null,
                null,
                NetworkAddressFamily.IPv4),
            (TransportProtocol.Udp, IpHelperApi.AfInet6) => new(
                Udp6Layout.RowSize,
                Udp6Layout.LocalAddressOffset,
                Udp6Layout.LocalScopeIdOffset,
                Udp6Layout.LocalPortOffset,
                Udp6Layout.ProcessIdOffset,
                null,
                null,
                null,
                null,
                NetworkAddressFamily.IPv6),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
    }

    private static LsofEntry? DecodeRow(
        TransportProtocol protocol,
        IntPtr row,
        NetworkRowLayout layout,
        ProcessCatalog catalog,
        ProcessSelection selection,
        IReadOnlyList<int> portFilters)
    {
        int owningProcess = Marshal.ReadInt32(row, layout.ProcessIdOffset);
        int localPort = ReadPortFromNetworkOrder(Marshal.ReadInt32(row, layout.LocalPortOffset));
        int remotePort = layout.RemotePortOffset is int remotePortOffset
            ? ReadPortFromNetworkOrder(Marshal.ReadInt32(row, remotePortOffset))
            : 0;
        bool hasRemotePort = layout.RemotePortOffset.HasValue && remotePort != 0;
        if (!selection.Includes(owningProcess) || !MatchesPort(portFilters, localPort, remotePort, hasRemotePort))
        {
            return null;
        }

        LsofEntry entry = catalog.CreateEntry(owningProcess);
        entry.Kind = LsofEntryKind.Connection;
        entry.AddressFamily = layout.AddressFamily;
        entry.Protocol = protocol == TransportProtocol.Tcp ? "TCP" : "UDP";
        entry.LocalPort = localPort;
        entry.LocalAddress = ReadAddress(row, layout.LocalAddressOffset, layout.LocalScopeIdOffset);

        if (layout.StateOffset is int stateOffset)
        {
            entry.State = TcpStateName(Marshal.ReadInt32(row, stateOffset));
        }

        if (layout.RemotePortOffset is not null)
        {
            entry.RemotePort = remotePort;
            entry.RemoteAddress = ReadAddress(row, layout.RemoteAddressOffset!.Value, layout.RemoteScopeIdOffset);
        }

        return entry;
    }

    private static bool MatchesPort(IReadOnlyList<int> ports, int localPort, int remotePort, bool hasRemotePort)
    {
        if (ports.Count == 0) return true;
        for (int i = 0; i < ports.Count; i++)
        {
            if (ports[i] == localPort || (hasRemotePort && ports[i] == remotePort)) return true;
        }
        return false;
    }

    private static bool TryReadRowCount(IntPtr buffer, int bufferSize, int rowSize, CollectionReport report, out int count)
    {
        if (bufferSize < sizeof(int))
        {
            count = 0;
            report.AddWarning("Warning: the network API returned a truncated table.");
            return false;
        }

        count = Marshal.ReadInt32(buffer);
        int maximumCount = (bufferSize - sizeof(int)) / rowSize;
        if (count < 0 || count > maximumCount)
        {
            report.AddWarning("Warning: the network API returned an invalid table size.");
            return false;
        }

        return true;
    }

    private static IntPtr QueryTable(
        TransportProtocol protocol,
        int family,
        int tableClass,
        IIpHelperApi nativeApi,
        INativeMemoryAllocator memoryAllocator,
        CollectionReport report,
        CancellationToken cancellationToken,
        out int resultSize)
    {
        resultSize = 0;
        int size = 0;
        cancellationToken.ThrowIfCancellationRequested();
        uint result = QueryNativeTable(protocol, nativeApi, IntPtr.Zero, ref size, family, tableClass);
        cancellationToken.ThrowIfCancellationRequested();
        if (result != IpHelperApi.ErrorInsufficientBuffer)
        {
            if (result != IpHelperApi.ErrorSuccess && result != IpHelperApi.ErrorNoData)
            {
                WarnTableQueryFailure(protocol, family, result, report);
            }

            return IntPtr.Zero;
        }

        for (int attempt = 0; attempt < MaximumTableQueryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (size <= 0 || size > MaximumTableBufferSize)
            {
                report.AddWarning("Warning: the network table exceeded the buffer size limit.");
                return IntPtr.Zero;
            }

            IntPtr buffer = memoryAllocator.Allocate(size);
            int bufferSize = size;
            bool returnBuffer = false;
            try
            {
                result = QueryNativeTable(protocol, nativeApi, buffer, ref bufferSize, family, tableClass);
                cancellationToken.ThrowIfCancellationRequested();
                if (result == IpHelperApi.ErrorSuccess)
                {
                    if (bufferSize < 0 || bufferSize > size)
                    {
                        report.AddWarning("Warning: the network API returned an invalid table size.");
                        return IntPtr.Zero;
                    }

                    resultSize = bufferSize;
                    returnBuffer = true;
                    return buffer;
                }

                if (result != IpHelperApi.ErrorInsufficientBuffer)
                {
                    if (result != IpHelperApi.ErrorNoData)
                    {
                        WarnTableQueryFailure(protocol, family, result, report);
                    }

                    return IntPtr.Zero;
                }

                size = NativeBufferSizing.GetNextSize(
                    size,
                    bufferSize,
                    MaximumTableBufferSize,
                    TableBufferGrowth);
                if (size == 0)
                {
                    report.AddWarning("Warning: the network table exceeded the buffer size limit.");
                    return IntPtr.Zero;
                }
            }
            finally
            {
                if (!returnBuffer)
                {
                    memoryAllocator.Free(buffer);
                }
            }
        }

        report.AddWarning("Warning: the network table changed too often to query within the retry limit.");
        return IntPtr.Zero;
    }

    private static uint QueryNativeTable(
        TransportProtocol protocol,
        IIpHelperApi nativeApi,
        IntPtr buffer,
        ref int size,
        int family,
        int tableClass)
    {
        return protocol switch
        {
            TransportProtocol.Tcp => nativeApi.GetExtendedTcpTable(buffer, ref size, false, family, tableClass, 0),
            TransportProtocol.Udp => nativeApi.GetExtendedUdpTable(buffer, ref size, false, family, tableClass, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(protocol))
        };
    }

    private static void WarnTableQueryFailure(TransportProtocol protocol, int family, uint error, CollectionReport report)
    {
        string addressFamily = family == IpHelperApi.AfInet ? "IPv4" : "IPv6";
        string protocolName = protocol switch
        {
            TransportProtocol.Tcp => "TCP",
            TransportProtocol.Udp => "UDP",
            _ => protocol.ToString()
        };
        report.AddWarning($"Warning: unable to read the {addressFamily} {protocolName} table (Windows error {error}).");
    }

    private static string ReadIPv4Address(IntPtr entry, int addressOffset)
    {
        return new IPAddress((long)(uint)Marshal.ReadInt32(entry, addressOffset)).ToString();
    }

    private static string ReadAddress(IntPtr entry, int addressOffset, int? scopeIdOffset)
    {
        return scopeIdOffset is int scopeOffset
            ? ReadIPv6Address(entry, addressOffset, scopeOffset)
            : ReadIPv4Address(entry, addressOffset);
    }

    private static string ReadIPv6Address(IntPtr entry, int addressOffset, int scopeIdOffset)
    {
        byte[] address = new byte[IPv6AddressLength];
        Marshal.Copy(IntPtr.Add(entry, addressOffset), address, 0, address.Length);
        long scopeId = (uint)Marshal.ReadInt32(entry, scopeIdOffset);
        return new IPAddress(address, scopeId).ToString();
    }

    private static int ReadPortFromNetworkOrder(int value)
    {
        // IP Helper stores the network-order port in the low 16 bits of a DWORD.
        return BinaryPrimitives.ReverseEndianness((ushort)value);
    }

    private static string TcpStateName(int state)
    {
        return (MibTcpState)state switch
        {
            MibTcpState.Closed => "Closed",
            MibTcpState.Listen => "Listen",
            MibTcpState.SynSent => "SynSent",
            MibTcpState.SynReceived => "SynReceived",
            MibTcpState.Established => "Established",
            MibTcpState.FinWait1 => "FinWait1",
            MibTcpState.FinWait2 => "FinWait2",
            MibTcpState.CloseWait => "CloseWait",
            MibTcpState.Closing => "Closing",
            MibTcpState.LastAck => "LastAck",
            MibTcpState.TimeWait => "TimeWait",
            MibTcpState.DeleteTcb => "DeleteTcb",
            _ => "Unknown"
        };
    }
}
