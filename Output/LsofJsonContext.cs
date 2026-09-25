using System.Text.Json.Serialization;
using Lsof.Models;

namespace Lsof.Output;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<LsofEntry>))]
internal sealed partial class LsofJsonContext : JsonSerializerContext
{
}
