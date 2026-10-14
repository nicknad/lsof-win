namespace Lsof.Collectors;

internal sealed record ModuleSnapshot(string[] Paths, int UnreadablePathCount);
