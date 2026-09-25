using Lsof.Models;
using Lsof.Output;
using System.Text.Json;

namespace Lsof.Tests.Models;

public sealed class LsofEntryTests
{
    [Fact]
    public void RemoveDuplicateEntries_keeps_the_first_exact_entry()
    {
        LsofEntry first = CreateEntry();
        LsofEntry duplicate = CreateEntry();
        LsofEntry different = CreateEntry();
        different.Name = "C:\\other.txt";

        List<LsofEntry> result = LsofEntryDeduplicator.RemoveDuplicateEntries([first, duplicate, different]);

        Assert.Equal(2, result.Count);
        Assert.Same(first, result[0]);
        Assert.Same(different, result[1]);
    }

    [Fact]
    public void RemoveDuplicateEntries_does_not_collide_on_delimiters_in_paths()
    {
        // Regression guard: these adjacent fields collide under delimiter-joined keys, but must stay distinct structurally.
        LsofEntry pathWithDelimiter = CreateEntry();
        pathWithDelimiter.ProcessPath = "C:\\tmp|TCP";
        pathWithDelimiter.Protocol = "UDP";

        LsofEntry delimiterInNextField = CreateEntry();
        delimiterInNextField.ProcessPath = "C:\\tmp";
        delimiterInNextField.Protocol = "TCP|UDP";

        List<LsofEntry> result = LsofEntryDeduplicator.RemoveDuplicateEntries([pathWithDelimiter, delimiterInNextField]);

        Assert.Equal(2, result.Count);
        Assert.Same(pathWithDelimiter, result[0]);
        Assert.Same(delimiterInNextField, result[1]);
    }

    [Fact]
    public void RemoveDuplicateEntries_keeps_distinct_handles_for_the_same_path_and_sorting_is_deterministic()
    {
        LsofEntry secondHandle = CreateEntry();
        secondHandle.ProcessName = "worker";
        secondHandle.Kind = LsofEntryKind.File;
        secondHandle.Name = "C:\\shared.txt";
        secondHandle.Handle = "0x2";

        LsofEntry firstHandle = CreateEntry();
        firstHandle.ProcessName = "worker";
        firstHandle.Kind = LsofEntryKind.File;
        firstHandle.Name = "C:\\shared.txt";
        firstHandle.Handle = "0x1";

        LsofEntry alpha = CreateEntry();
        alpha.ProcessName = "alpha";
        LsofEntry zeta = CreateEntry();
        zeta.ProcessName = "zeta";

        List<LsofEntry> unique = LsofEntryDeduplicator.RemoveDuplicateEntries([secondHandle, zeta, firstHandle, alpha]);
        unique.Sort(LsofEntryComparer.Instance);

        Assert.Collection(
            unique,
            entry => Assert.Same(alpha, entry),
            entry => Assert.Same(firstHandle, entry),
            entry => Assert.Same(secondHandle, entry),
            entry => Assert.Same(zeta, entry));
    }

    [Fact]
    public void Json_keeps_the_display_category_without_serializing_internal_discriminators()
    {
        LsofEntry entry = CreateEntry();
        entry.Kind = LsofEntryKind.Connection;
        entry.AddressFamily = NetworkAddressFamily.IPv6;
        string json = JsonSerializer.Serialize(entry, LsofJsonContext.Default.LsofEntry);
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal("IPv6", document.RootElement.GetProperty("Category").GetString());
        Assert.False(document.RootElement.TryGetProperty("Kind", out _));
        Assert.False(document.RootElement.TryGetProperty("AddressFamily", out _));
    }

    [Fact]
    public void Summary_counts_entries_by_typed_kind()
    {
        LsofEntry connection = CreateEntry();
        connection.Kind = LsofEntryKind.Connection;
        connection.AddressFamily = NetworkAddressFamily.IPv4;
        LsofEntry file = CreateEntry();
        file.Kind = LsofEntryKind.File;
        LsofEntry module = CreateEntry();
        module.Kind = LsofEntryKind.Module;

        LsofSummary summary = LsofSummary.FromEntries([connection, file, module]);

        Assert.Equal(1, summary.Connections);
        Assert.Equal(1, summary.Files);
        Assert.Equal(1, summary.Modules);
    }

    private static LsofEntry CreateEntry()
    {
        return new LsofEntry
        {
            ProcessId = 4321,
            ProcessName = "process",
            ProcessPath = "C:\\process.exe",
            Kind = LsofEntryKind.File,
            Name = "C:\\file.txt",
            Handle = "0x10"
        };
    }
}
