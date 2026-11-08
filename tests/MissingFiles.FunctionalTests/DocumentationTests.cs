using System.Text.RegularExpressions;

using MissingFiles.Cli;

namespace MissingFiles.FunctionalTests;

/// <summary>The user documentation in <c>README.md</c> and <c>docs\UserGuide.md</c> (spec WP8).</summary>
public sealed class DocumentationTests
{
    private const string Readme = "README.md";
    private const string UserGuide = @"docs\UserGuide.md";
    private const string DataFolderVariable = "MISSINGFILES_DATA_FOLDER";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static TheoryData<string> Documents => new() { Readme, UserGuide };

    [Theory]
    [MemberData(nameof(Documents))]
    public void ExplainsTheSmartScreenWarning(string document) // NFR-10a
    {
        var text = Read(document);

        Assert.Contains("SmartScreen", text, StringComparison.Ordinal);
        Assert.Contains("**More info → Run anyway**", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DataFolderVariableIsOnlyInTheDeveloperSectionOfTheReadme() // REQ-04a
    {
        var sections = Read(Readme).Split("\n## ");

        Assert.Contains(sections, s => s.StartsWith("Build and test", StringComparison.Ordinal) && s.Contains(DataFolderVariable, StringComparison.Ordinal));
        Assert.All(
            sections.Where(s => s.Contains(DataFolderVariable, StringComparison.Ordinal)),
            s => Assert.StartsWith("Build and test", s, StringComparison.Ordinal));
        Assert.DoesNotContain(DataFolderVariable, Read(UserGuide), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void LinksToFilesAndSectionsExist(string document)
    {
        var links = new Regex(@"\]\((?<target>[^)\s]+)\)", RegexOptions.None, RegexTimeout).Matches(Read(document))
            .Select(m => m.Groups["target"].Value)
            .Where(t => !t.StartsWith("https://", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(links);
        Assert.All(links, link =>
        {
            var hash = link.IndexOf('#', StringComparison.Ordinal);
            var file = hash < 0 ? link : link[..hash];
            var target = file.Length == 0
                ? document
                : Path.GetRelativePath(Repository.Root, Path.GetFullPath(Path.Join(Repository.Root, Path.GetDirectoryName(document), file)));
            Assert.True(Path.Exists(Path.Join(Repository.Root, target)), $"{document}: {link} points to a file that does not exist.");
            if (hash >= 0)
            {
                Assert.True(Anchors(Read(target)).Contains(link[(hash + 1)..]), $"{document}: {link} points to a section that does not exist.");
            }
        });
    }

    [Fact]
    public void UserGuideDescribesEveryCommandLineOption() // REQ-33, REQ-38
    {
        var options = new Regex("--[a-z][a-z-]*", RegexOptions.None, RegexTimeout)
            .Matches(Program.Usage + ScanCommand.Usage + CopyCommand.Usage)
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        Assert.Equal(10, options.Count);
        Assert.All(options, option => Assert.Contains($"| `{option}", Read(UserGuide), StringComparison.Ordinal));
    }

    [Fact]
    public void UserGuideListsEveryExitCode() // REQ-35
    {
        var guide = Read(UserGuide);

        Assert.All(Enum.GetValues<ExitCode>(), code => Assert.Matches($@"(?m)^\| {(int)code} \|", guide));
    }

    [Fact]
    public void UserGuideListsTheContentOfTheZip() // NFR-10
    {
        var guide = Read(UserGuide);

        Assert.Contains("`MissingFiles-XX.YY.ZZ.BB-win-x64.zip`", guide, StringComparison.Ordinal);
        Assert.All(PackageTests.ZipContent, file => Assert.Contains($"| `{file}` |", guide, StringComparison.Ordinal));
    }

    [Fact]
    public void UserGuideShowsTheBuiltInFileTypesFile() // REQ-05, REQ-05b
    {
        var section = Read(UserGuide).Split("\n## File types file", 2)[1];
        var example = new Regex("```json\r?\n(?<json>.*?)```", RegexOptions.Singleline, RegexTimeout).Match(section);

        Assert.True(example.Success, "The section 'File types file' has no JSON example.");
        Assert.Equal(
            Normalize(File.ReadAllText(Path.Join(Repository.Root, "src", "MissingFiles.Core", "FileTypes", "FileTypes.default.json"))),
            Normalize(example.Groups["json"].Value));
    }

    private static string Read(string document) => File.ReadAllText(Path.Join(Repository.Root, document));

    private static string Normalize(string text) => text.ReplaceLineEndings("\n").Trim();

    /// <summary>The anchors that GitHub makes for the headings of a Markdown document.</summary>
    private static HashSet<string> Anchors(string markdown) =>
        new Regex(@"(?m)^#+ (?<heading>.+?)\r?$", RegexOptions.None, RegexTimeout).Matches(markdown)
            .Select(m => new string([.. m.Groups["heading"].Value.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')]).Replace(' ', '-'))
            .ToHashSet(StringComparer.Ordinal);
}
