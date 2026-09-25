using System.Runtime.InteropServices;
using Lsof.Native;

namespace Lsof.Tests.Support;

internal delegate uint FakeQueryHandler(IntPtr table, ref int size);

internal sealed record IpHelperCall(
    bool IsTcp,
    int Family,
    IntPtr Buffer,
    int InputSize,
    int TableClass,
    bool Order,
    int Reserved);

internal sealed class FakeIpHelperApi : IIpHelperApi
{
    private readonly Dictionary<(bool IsTcp, int Family), FakeQueryHandler> _handlers = new();

    public List<IpHelperCall> Calls { get; } = new();

    public void SetTable(bool isTcp, int family, byte[] table, int? reportedSize = null)
    {
        _handlers[(isTcp, family)] = (IntPtr buffer, ref int size) =>
        {
            if (buffer == IntPtr.Zero)
            {
                size = table.Length;
                return IpHelperApi.ErrorInsufficientBuffer;
            }

            if (size < table.Length)
            {
                size = table.Length;
                return IpHelperApi.ErrorInsufficientBuffer;
            }

            Marshal.Copy(table, 0, buffer, table.Length);
            size = reportedSize ?? table.Length;
            return IpHelperApi.ErrorSuccess;
        };
    }

    public void SetHandler(bool isTcp, int family, FakeQueryHandler handler)
    {
        _handlers[(isTcp, family)] = handler;
    }

    public uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, bool order, int family, int tableClass, int reserved)
    {
        return Query(true, tcpTable, ref size, family, tableClass, order, reserved);
    }

    public uint GetExtendedUdpTable(IntPtr udpTable, ref int size, bool order, int family, int tableClass, int reserved)
    {
        return Query(false, udpTable, ref size, family, tableClass, order, reserved);
    }

    private uint Query(bool isTcp, IntPtr table, ref int size, int family, int tableClass, bool order, int reserved)
    {
        Calls.Add(new IpHelperCall(isTcp, family, table, size, tableClass, order, reserved));
        return _handlers.TryGetValue((isTcp, family), out FakeQueryHandler? handler)
            ? handler(table, ref size)
            : IpHelperApi.ErrorNoData;
    }
}
