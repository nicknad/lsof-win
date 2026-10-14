using System.Globalization;
using System.Text.RegularExpressions;
using Lsof.Models;

namespace Lsof.Processes;

internal sealed class ProcessFilter
{
    private const int ExecutableSuffixLength = 4; // Length of the ".exe" suffix.

    private readonly string[] _processFilters;
    private readonly string[] _userFilters;
    private readonly IProcessOwnerProvider? _ownerProvider;
    private readonly HashSet<int> _processIdFilters = new();
    private readonly string[] _processNameFilters;
    private readonly Dictionary<(string Pattern, bool StripExeSuffix), Regex> _regexCache = new();

    public ProcessFilter(List<string> filters)
        : this(filters, Array.Empty<string>(), null)
    {
    }

    public ProcessFilter(IReadOnlyList<string> filters, IReadOnlyList<string> userFilters, IProcessOwnerProvider? ownerProvider)
    {
        _processFilters = filters.ToArray();
        _userFilters = userFilters.ToArray();
        _ownerProvider = ownerProvider;
        List<string> processNameFilters = new(filters.Count);
        foreach (string filter in _processFilters)
        {
            if (int.TryParse(filter, NumberStyles.Integer, CultureInfo.InvariantCulture, out int processId))
            {
                _processIdFilters.Add(processId);
            }
            else
            {
                processNameFilters.Add(filter);
            }
        }

        _processNameFilters = processNameFilters.ToArray();
    }

    public ProcessSelection ResolveSelection(ProcessCatalog catalog, CollectionReport report, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_processFilters.Length == 0 && _userFilters.Length == 0)
        {
            return ProcessSelection.AllProcesses;
        }

        HashSet<int> processIds = new();
        bool matchedRunningProcess = false;
        foreach (ProcessInfo info in catalog.ProcessInfos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (MatchesProcessFilter(info.ProcessId, info.Name) || MatchesUserFilter(info.ProcessId))
            {
                matchedRunningProcess = true;
                processIds.Add(info.ProcessId);
            }
        }

        if (!matchedRunningProcess)
        {
            report.AddInformation("No running process matches: " + string.Join(", ", DescribeFilters()));
        }
        return ProcessSelection.FromProcessIds(processIds);
    }

    private IEnumerable<string> DescribeFilters()
    {
        return _processFilters.Concat(_userFilters);
    }

    private bool MatchesProcessFilter(int processId, string processName)
    {
        if (_processFilters.Length == 0)
        {
            return false;
        }

        if (_processIdFilters.Contains(processId))
        {
            return true;
        }

        foreach (string filter in _processNameFilters)
        {
            if (NameMatches(filter, processName))
            {
                return true;
            }
        }
        return false;
    }

    private bool MatchesUserFilter(int processId)
    {
        if (_userFilters.Length == 0 || _ownerProvider is null)
        {
            return false;
        }

        string? owner = _ownerProvider.GetOwner(processId);
        if (string.IsNullOrEmpty(owner))
        {
            return false;
        }

        foreach (string filter in _userFilters)
        {
            if (UserMatches(owner, filter))
            {
                return true;
            }
        }
        return false;
    }

    private static bool UserMatches(string owner, string filter)
    {
        if (string.Equals(owner, filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Also accept a bare account name when the owner is domain-qualified ("DOMAIN\user").
        int separator = owner.LastIndexOf('\\');
        return separator >= 0 && owner.AsSpan(separator + 1).Equals(filter.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    public bool Matches(int processId, string processName)
    {
        return _processFilters.Length == 0 || MatchesProcessFilter(processId, processName);
    }

    private bool NameMatches(string pattern, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        ReadOnlySpan<char> processName = name.AsSpan();
        bool nameHasExeSuffix = HasExeSuffix(processName);
        bool patternHasExeSuffix = HasExeSuffix(pattern.AsSpan());
        ReadOnlySpan<char> baseName = StripExeSuffix(processName);

        if (WildcardMatch(baseName, pattern, patternHasExeSuffix))
        {
            return true;
        }

        return (nameHasExeSuffix || patternHasExeSuffix)
            && WildcardMatch(processName, pattern, stripExeSuffix: false);
    }

    private static ReadOnlySpan<char> StripExeSuffix(ReadOnlySpan<char> value)
    {
        return HasExeSuffix(value)
            ? value[..^ExecutableSuffixLength]
            : value;
    }

    private static bool HasExeSuffix(ReadOnlySpan<char> value)
    {
        return value.Length >= ExecutableSuffixLength
            && value[^ExecutableSuffixLength..].Equals(".exe".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private bool WildcardMatch(ReadOnlySpan<char> input, string pattern, bool stripExeSuffix)
    {
        (string Pattern, bool StripExeSuffix) cacheKey = (pattern, stripExeSuffix);
        if (!_regexCache.TryGetValue(cacheKey, out Regex? regex))
        {
            string effectivePattern = stripExeSuffix ? pattern[..^ExecutableSuffixLength] : pattern;
            // Translate the wildcard pattern into an anchored regex (* -> .*, ? -> .) and cache it;
            // NonBacktracking keeps matching linear-time on hostile input.
            string expression = "^" + Regex.Escape(effectivePattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            regex = new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
            _regexCache.Add(cacheKey, regex);
        }

        return regex.EnumerateMatches(input).MoveNext();
    }
}
