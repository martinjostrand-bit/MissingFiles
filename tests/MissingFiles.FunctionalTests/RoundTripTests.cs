using MissingFiles.Cli;
using MissingFiles.Core.Scanning;

namespace MissingFiles.FunctionalTests;

/// <summary>
/// FT-08: for each test case, scan → copy → scan again through the CLI. The second scan finds
/// nothing missing, except files skipped by design (TC-15). Every copied file is checked
/// against its source (FT-03).
/// </summary>
public sealed class RoundTripTests : IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Theory]
    [MemberData(nameof(ScanCases.ScanningIds), MemberType = typeof(ScanCases))]
    public void ScanCopyScanLeavesOnlyFilesSkippedByDesign(string id)
    {
        var testCase = ScanCases.Get(id);
        testCase.Arrange(_folder);
        var prepared = testCase.Prepare(_folder);
        try
        {
            var options = testCase.Options(_folder);
            var skippedBefore = testCase.SkippedByCopy.ToDictionary(p => p, p => File.ReadAllBytes(Path.Join(prepared.Destination, p)));

            var firstScan = Scan(prepared, options, "scan1");
            var copy = Cli.Run(_folder, ["copy", "--result", firstScan, "--out", Path.Join(_folder.Root, "logs")]);
            var secondScan = Scan(prepared, options, "scan2");

            var expectedCopyCode = testCase.SkippedByCopy.Length > 0 ? ExitCode.SuccessWithFindings : ExitCode.Success;
            Assert.True(expectedCopyCode == copy.Code, $"{testCase}: copy exit code {copy.Code}.\n{copy.Out}\n{copy.Error}");

            // FT-03: identical content and timestamp at the same relative path.
            foreach (var path in testCase.Missing.Except(testCase.SkippedByCopy))
            {
                var source = Path.Join(prepared.Source, path);
                var target = Path.Join(prepared.Destination, path);
                Assert.True(File.Exists(target), $"{testCase}: {path} was not copied.");
                Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(target));
                Assert.Equal(File.GetLastWriteTimeUtc(source), File.GetLastWriteTimeUtc(target));
            }

            // REQ-26: never overwritten.
            foreach (var (path, content) in skippedBefore)
            {
                Assert.Equal(content, File.ReadAllBytes(Path.Join(prepared.Destination, path)));
            }

            Assert.Empty(Directory.GetFiles(prepared.Destination, "*.missingfiles-tmp", SearchOption.AllDirectories)); // REQ-28

            var second = ScanResultFile.Load(secondScan);
            Assert.Equal(
                testCase.SkippedByCopy.Order(StringComparer.OrdinalIgnoreCase),
                second.MissingFiles.Select(m => m.RelativePath).Order(StringComparer.OrdinalIgnoreCase));
            Assert.Equal(testCase.Summary!.SourceFilesScanned, second.Summary.SourceFilesScanned);
            Assert.Equal(testCase.ErrorFolders.Length, second.Summary.Errors);
        }
        finally
        {
            prepared.Cleanup?.Dispose();
        }
    }

    private string Scan(PreparedCase prepared, string[] options, string outFolder)
    {
        var output = Path.Join(_folder.Root, outFolder);
        var run = Cli.Run(_folder, ["scan", "--source", prepared.Source, "--dest", prepared.Destination, "--out", output, .. options]);
        Assert.True(run.Code is ExitCode.Success or ExitCode.SuccessWithFindings or ExitCode.CompletedWithErrors, $"{run.Code}\n{run.Error}");
        return Assert.Single(Directory.GetFiles(output, "MissingFiles_*.json"));
    }
}
