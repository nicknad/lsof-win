namespace Lsof.Collectors;

// Matches resolved file-handle paths against the positional path filters from the command line.
// Paths are normalized to full paths (relative filters resolve against the current directory) and
// compared case-insensitively, matching Windows path semantics.
internal sealed class FileFilter
{
    private readonly string[] _paths;

    public static FileFilter Empty { get; } = new(Array.Empty<string>());

    public FileFilter(IReadOnlyList<string> filters)
    {
        _paths = new string[filters.Count];
        for (int i = 0; i < filters.Count; i++)
        {
            _paths[i] = Normalize(filters[i]);
        }
    }

    public bool IsEmpty => _paths.Length == 0;

    public bool Matches(string path)
    {
        if (_paths.Length == 0)
        {
            return true;
        }

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string normalized = Normalize(path);
        foreach (string candidate in _paths)
        {
            if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string Normalize(string path)
    {
        string normalized;
        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            normalized = path;
        }

        normalized = normalized.Replace('/', '\\');
        return normalized.Length > 3 && normalized.EndsWith('\\')
            ? normalized.TrimEnd('\\')
            : normalized;
    }
}
