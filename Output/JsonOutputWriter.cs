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
        // JsonSerializer.Serialize with the source-generated context keeps serialization
        // reflection-free so the AOT-published executable works.
        output.WriteLine(JsonSerializer.Serialize(entries, LsofJsonContext.Default.ListLsofEntry));
    }
}
