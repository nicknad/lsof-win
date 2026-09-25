using System.Text.Json;
using Lsof.Models;

namespace Lsof.Output;

internal static class JsonOutputWriter
{
    public static void Write(List<LsofEntry> entries)
    {
        Write(entries, Console.Out);
    }

    internal static void Write(List<LsofEntry> entries, TextWriter output)
    {
        output.WriteLine(JsonSerializer.Serialize(entries, LsofJsonContext.Default.ListLsofEntry));
    }
}
