namespace Lsof.Native;

// Instance wrapper over the static P/Invoke methods so collectors can accept a fake in tests.
internal sealed class WindowsIpHelperApi : IIpHelperApi
{
    public uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, bool order, int family, int tableClass, int reserved) =>
        IpHelperApi.GetExtendedTcpTable(tcpTable, ref size, order, family, tableClass, reserved);

    public uint GetExtendedUdpTable(IntPtr udpTable, ref int size, bool order, int family, int tableClass, int reserved) =>
        IpHelperApi.GetExtendedUdpTable(udpTable, ref size, order, family, tableClass, reserved);
}
