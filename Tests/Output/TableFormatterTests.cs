using Lsof.Output;

namespace Lsof.Tests.Output;

public sealed class TableFormatterTests
{
    [Theory]
    [InlineData("127.0.0.1", 80, "127.0.0.1:80")]
    [InlineData("::1", 443, "[::1]:443")]
    [InlineData("fe80::1%9", 53, "[fe80::1%9]:53")]
    [InlineData("", 80, "")]
    [InlineData("::1", 0, "")]
    public void FormatEndpoint_uses_address_syntax_to_bracket_ipv6(string address, int port, string expected)
    {
        Assert.Equal(expected, TableFormatter.FormatEndpoint(address, port));
    }
}
