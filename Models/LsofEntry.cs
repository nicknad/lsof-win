using System.Text.Json.Serialization;

namespace Lsof.Models;

internal sealed class LsofEntry
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public string ProcessPath { get; set; } = "";

    [JsonIgnore]
    public LsofEntryKind Kind { get; set; }

    [JsonIgnore]
    public NetworkAddressFamily? AddressFamily { get; set; }

    public string Category => Kind switch
    {
        LsofEntryKind.File => "File",
        LsofEntryKind.Connection when AddressFamily is NetworkAddressFamily family => family.ToString(),
        LsofEntryKind.Module => "Module",
        _ => ""
    };

    public string Protocol { get; set; } = "";
    public string LocalAddress { get; set; } = "";
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = "";
    public int RemotePort { get; set; }
    public string State { get; set; } = "";
    public string Handle { get; set; } = "";
    public string Name { get; set; } = "";
}
