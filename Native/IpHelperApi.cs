using System.Runtime.InteropServices;

namespace Lsof.Native;

internal static class IpHelperApi
{
    public const int AfInet = 2; // AF_INET address family.
    public const int AfInet6 = 23; // AF_INET6 address family.
    public const int TcpTableOwnerPidAll = 5; // TCP_TABLE_OWNER_PID_ALL table class.
    public const int UdpTableOwnerPid = 1; // UDP_TABLE_OWNER_PID table class.
    public const uint ErrorSuccess = 0; // NO_ERROR.
    public const uint ErrorInsufficientBuffer = 122; // ERROR_INSUFFICIENT_BUFFER.
    public const uint ErrorNoData = 232; // ERROR_NO_DATA.

    // iphlpapi!GetExtendedTcpTable: fills a caller buffer with the requested TCP table.
    // TCP_TABLE_OWNER_PID_ALL returns every IPv4/IPv6 TCP endpoint together with its owning PID.
    // Call once with a null/zero-size buffer to learn the required size: that call returns
    // ERROR_INSUFFICIENT_BUFFER (122); success is NO_ERROR (0) and an empty table is ERROR_NO_DATA (232).
    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, bool order, int family, int tableClass, int reserved);

    // iphlpapi!GetExtendedUdpTable: UDP equivalent of GetExtendedTcpTable.
    // UDP_TABLE_OWNER_PID lists UDP endpoints and their owning PID, with the same error convention.
    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern uint GetExtendedUdpTable(IntPtr udpTable, ref int size, bool order, int family, int tableClass, int reserved);
}

internal interface IIpHelperApi
{
    uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, bool order, int family, int tableClass, int reserved);

    uint GetExtendedUdpTable(IntPtr udpTable, ref int size, bool order, int family, int tableClass, int reserved);
}

// Instance wrapper over the static P/Invoke methods so collectors can accept a fake in tests.
internal sealed class WindowsIpHelperApi : IIpHelperApi
{
    public uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, bool order, int family, int tableClass, int reserved)
    {
        return IpHelperApi.GetExtendedTcpTable(tcpTable, ref size, order, family, tableClass, reserved);
    }

    public uint GetExtendedUdpTable(IntPtr udpTable, ref int size, bool order, int family, int tableClass, int reserved)
    {
        return IpHelperApi.GetExtendedUdpTable(udpTable, ref size, order, family, tableClass, reserved);
    }
}
