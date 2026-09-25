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

    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, bool order, int family, int tableClass, int reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern uint GetExtendedUdpTable(IntPtr udpTable, ref int size, bool order, int family, int tableClass, int reserved);
}

internal interface IIpHelperApi
{
    uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, bool order, int family, int tableClass, int reserved);

    uint GetExtendedUdpTable(IntPtr udpTable, ref int size, bool order, int family, int tableClass, int reserved);
}

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
