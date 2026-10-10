using System.Globalization;
using System.Text.Json;

namespace MissingFiles.FunctionalTests;

/// <summary>
/// FT-02: a scan through the CLI for each test case TC-01..TC-23, checking the exit code,
/// the counters and the content of the scan result file against the expected result.
/// </summary>
public sealed class ScanScenarioTests : IDisposable
{
    /// <summary>The fields of <c>summary</c> in the order of <see cref="Core.Scanning.ScanSummary"/> (REQ-19).</summary>
    private static readonly string[] SummaryFields =
        ["sourceFilesScanned", "matched", "missing", "errors", "duplicateNameGroups", "sameNameDifferentSize"];

    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Theory]
    [MemberData(nameof(ScanCases.Ids), MemberType = typeof(ScanCases))]
    public void ScanGivesExpectedResult(string id)
    {
        var testCase = ScanCases.Get(id);
        testCase.Arrange(_folder);
        var prepared = testCase.Prepare(_folder);
        try
        {
            var run = Cli.Run(_folder, ["scan", "--source", prepared.Source, "--dest", prepared.Destination, .. testCase.Options(_folder)]);

            Assert.True(testCase.ExpectedExitCode == run.Code, $"{testCase}: exit code {run.Code}, expected {testCase.ExpectedExitCode}.\n{run.Out}\n{run.Error}");
            if (testCase.IsRejected)
            {
                Assert.Contains(testCase.RejectedWith!, run.Error, StringComparison.Ordinal);
                Assert.Empty(_folder.OutputFiles("*"));
                return;
            }

            var resultFile = Assert.Single(_folder.OutputFiles("MissingFiles_*.json"));
            Assert.Contains(resultFile, run.Out, StringComparison.Ordinal);
            CheckResultFile(testCase, prepared, resultFile);
        }
        finally
        {
            prepared.Cleanup?.Dispose();
        }
    }

    /// <summary>Checks the JSON as written (REQ-19), not as read back by the application.</summary>
    private void CheckResultFile(ScanCase testCase, PreparedCase prepared, string resultFile)
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(resultFile));
        var root = json.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.StartsWith("MissingFiles ", root.GetProperty("application").GetString(), StringComparison.Ordinal);
        Assert.Equal("NameAndSize", root.GetProperty("matchMode").GetString());
        Assert.True(root.GetProperty("scanFinished").GetDateTimeOffset() >= root.GetProperty("scanStarted").GetDateTimeOffset());
        Assert.Equal(Path.GetFullPath(prepared.Source), root.GetProperty("sourceRoot").GetString());
        Assert.Equal(prepared.Destination, root.GetProperty("destinationRoot").GetString());
        // REQ-06a: the file used; null for the built-in default (no --types-file and no user file).
        var expectedTypesFile = testCase.Options(_folder) is ["--types-file", var typesFile, ..] ? typesFile : null;
        Assert.Equal(expectedTypesFile, root.GetProperty("fileTypesFile").GetString());

        if (testCase.Extensions is { } extensions)
        {
            Assert.Equal(extensions, root.GetProperty("extensions").EnumerateArray().Select(e => e.GetString()));
        }

        var summary = root.GetProperty("summary");
        var expected = testCase.Summary!;
        Assert.Equal(
            [expected.SourceFilesScanned, expected.Matched, expected.Missing, expected.Errors, expected.DuplicateNameGroups, expected.SameNameDifferentSize],
            SummaryFields.Select(name => summary.GetProperty(name).GetInt32()));

        var missing = root.GetProperty("missingFiles").EnumerateArray().ToList();
        Assert.Equal(Sorted(testCase.Missing), Sorted(missing.Select(m => m.GetProperty("relativePath").GetString()!)));
        Assert.Equal(Sorted(testCase.Flagged), Sorted(missing.Where(m => m.GetProperty("sameNameDifferentSize").GetBoolean()).Select(m => m.GetProperty("relativePath").GetString()!)));
        foreach (var file in missing)
        {
            var source = new FileInfo(Path.Join(prepared.Source, file.GetProperty("relativePath").GetString()));
            Assert.Equal(source.Length, file.GetProperty("sizeBytes").GetInt64());
            Assert.Equal(
                source.LastWriteTimeUtc,
                DateTime.Parse(file.GetProperty("lastWriteTimeUtc").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }

        var errors = root.GetProperty("errors").EnumerateArray().ToList();
        Assert.Equal(
            Sorted(testCase.ErrorFolders.Select(f => Path.Join(prepared.Source, f))),
            Sorted(errors.Select(e => e.GetProperty("path").GetString()!)));
        Assert.All(errors, e => Assert.False(string.IsNullOrEmpty(e.GetProperty("message").GetString())));
    }

    private static List<string> Sorted(IEnumerable<string> values) => [.. values.Order(StringComparer.OrdinalIgnoreCase)];
}
