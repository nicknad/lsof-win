namespace Lsof.Models;

internal enum LsofEntryKind
{
    Unknown,
    File,
    Connection,
    Module
}

internal enum NetworkAddressFamily
{
    IPv4 = 4,
    IPv6 = 6
}
