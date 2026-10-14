using Lsof.Collectors;

namespace Lsof.Tests.Collectors;

public sealed class FileFilterTests
{
    [Fact]
    public void Matches_returns_true_for_every_path_when_no_filter_is_supplied()
    {
        FileFilter filter = FileFilter.Empty;

        Assert.True(filter.IsEmpty);
        Assert.True(filter.Matches("C:\\anything\\at\\all.txt"));
    }

    [Fact]
    public void Matches_returns_false_for_an_empty_path_when_a_filter_is_supplied()
    {
        FileFilter filter = new(["C:\\temp\\a.txt"]);

        Assert.False(filter.Matches(""));
    }

    [Theory]
    [InlineData("C:\\Temp\\File.txt", "C:\\TEMP\\file.TXT")]
    [InlineData("C:/Temp/File.txt", "C:\\Temp\\file.txt")]
    public void Matches_compares_full_paths_case_insensitively_and_normalizes_separators(string filterPath, string handlePath)
    {
        FileFilter filter = new([filterPath]);

        Assert.True(filter.Matches(handlePath));
    }

    [Fact]
    public void Matches_resolves_relative_filters_against_the_current_directory()
    {
        string relative = Path.Combine("nested", "file.txt");
        string absolute = Path.GetFullPath(relative);

        FileFilter filter = new([relative]);

        Assert.True(filter.Matches(absolute));
    }

    [Fact]
    public void Matches_rejects_paths_that_differ_from_the_filter()
    {
        FileFilter filter = new(["C:\\temp\\wanted.txt"]);

        Assert.False(filter.Matches("C:\\temp\\other.txt"));
    }
}
