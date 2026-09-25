using System.Text.Json;
using Lsof.Models;
using Lsof.Output;

namespace Lsof.Tests.Output;

public sealed class JsonOutputWriterTests
{
    [Fact]
    public void Write_serializes_entries_to_the_supplied_writer()
    {
        LsofEntry entry = new()
        {
            ProcessId = 7,
            ProcessName = "fixture",
            Kind = LsofEntryKind.Connection,
            AddressFamily = NetworkAddressFamily.IPv4,
            Protocol = "TCP",
            LocalAddress = "127.0.0.1",
            LocalPort = 8080
        };
        using StringWriter output = new();

        JsonOutputWriter.Write([entry], output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement row = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(7, row.GetProperty("ProcessId").GetInt32());
        Assert.Equal("TCP", row.GetProperty("Protocol").GetString());
    }
}
