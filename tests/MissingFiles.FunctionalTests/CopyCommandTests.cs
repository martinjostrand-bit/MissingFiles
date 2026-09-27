using MissingFiles.Cli;
using MissingFiles.Core.Copying;

namespace MissingFiles.FunctionalTests;

/// <summary>The <c>copy</c> command (spec REQ-33, REQ-35, FT-03..FT-08 through the CLI).</summary>
public sealed class CopyCommandTests : IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void CopiesMissingFilesAndWritesLog() // FT-03
    {
        _folder.AddBoth("IMG_0001.JPG");
        _folder.AddSource(@"2021\Bjørn æøå.JPG", size: 123);
        var resultFile = ScanAndGetResultFile();

        var run = Copy(resultFile);

        Assert.Equal(ExitCode.Success, run.Code);
        Assert.Equal(123, new FileInfo(Path.Join(_folder.Destination, @"2021\Bjørn æøå.JPG")).Length);
        Assert.Matches(@"Copied:\s+1", run.Out);
        var log = Assert.Single(_folder.OutputFiles("MissingFilesCopy_*.log"));
        Assert.Contains(log, run.Out, StringComparison.Ordinal);
        Assert.Contains(resultFile, File.ReadAllText(log), StringComparison.Ordinal);
    }

    [Fact]
    public void SecondCopySkipsEverythingAndReturnsFindings() // FT-04
    {
        _folder.AddSource("IMG_0001.JPG");
        var resultFile = ScanAndGetResultFile();
        Copy(resultFile);

        var second = Copy(resultFile);

        Assert.Equal(ExitCode.SuccessWithFindings, second.Code);
        Assert.Matches(@"Skipped - exists:\s+1", second.Out);
    }

    [Fact]
    public void DryRunWritesNoFilesButALog() // FT-07
    {
        _folder.AddSource("IMG_0001.JPG");
        var resultFile = ScanAndGetResultFile();

        var run = Copy(resultFile, "--dry-run");

        Assert.Equal(ExitCode.Success, run.Code);
        Assert.Contains("DRY RUN", run.Out, StringComparison.Ordinal);
        Assert.Matches(@"Would copy:\s+1", run.Out);
        Assert.Empty(Directory.GetFileSystemEntries(_folder.Destination));
        Assert.Contains("DRY RUN", File.ReadAllText(Assert.Single(_folder.OutputFiles("*.log"))), StringComparison.Ordinal);
    }

    [Fact]
    public void FailedFilesReturnErrorsAndAreListedOnStandardError() // REQ-29
    {
        var locked = _folder.AddSource("IMG_0001.JPG");
        var resultFile = ScanAndGetResultFile();

        CliRun run;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            run = Copy(resultFile);
        }

        Assert.Equal(ExitCode.CompletedWithErrors, run.Code);
        Assert.Contains("1 file(s) could not be copied", run.Error, StringComparison.Ordinal);
        Assert.Contains("IMG_0001.JPG", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelledCopyStillWritesLog() // FT-06, REQ-34
    {
        _folder.AddSource("IMG_0001.JPG");
        var resultFile = ScanAndGetResultFile();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var run = Cli.Run(_folder, ["copy", "--result", resultFile], cancellationToken: cts.Token);

        Assert.Equal(ExitCode.Cancelled, run.Code);
        Assert.Contains("Copy cancelled", run.Out, StringComparison.Ordinal);
        Assert.Contains("Cancelled:                 Yes", File.ReadAllText(Assert.Single(_folder.OutputFiles("*.log"))), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(_folder.Destination));
    }

    [Fact]
    public void MissingResultFileIsRejected()
    {
        var run = Copy(Path.Join(_folder.Root, "nope.json"));

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains("could not be read", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidResultFileIsRejected()
    {
        var run = Copy(_folder.WriteFile("broken.json", "{ nope"));

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains("Invalid JSON", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovedDestinationFolderIsRejected()
    {
        _folder.AddSource("IMG_0001.JPG");
        var resultFile = ScanAndGetResultFile();
        Directory.Delete(_folder.Destination);

        var run = Copy(resultFile);

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains("Destination folder does not exist", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanCopyScanThroughTheCli() // FT-08
    {
        _folder.AddBoth("IMG_0001.JPG");
        _folder.AddSource(@"a\IMG_0002.JPG");
        _folder.AddSource(@"b\c\VID_0001.MP4", size: 500);

        var first = Cli.Scan(_folder, "--out", Path.Join(_folder.Root, "scan1"));
        var copy = Copy(Directory.GetFiles(Path.Join(_folder.Root, "scan1"))[0]);
        var second = Cli.Scan(_folder, "--out", Path.Join(_folder.Root, "scan2"));

        Assert.Equal(ExitCode.SuccessWithFindings, first.Code);
        Assert.Equal(ExitCode.Success, copy.Code);
        Assert.Equal(ExitCode.Success, second.Code);
    }

    [Theory]
    [InlineData(false, 0, 0, 0, ExitCode.Success)]
    [InlineData(false, 1, 0, 0, ExitCode.SuccessWithFindings)]
    [InlineData(false, 0, 1, 0, ExitCode.SuccessWithFindings)]
    [InlineData(false, 1, 1, 1, ExitCode.CompletedWithErrors)]
    [InlineData(true, 0, 0, 1, ExitCode.Cancelled)]
    public void ExitCodeFollowsSpecification(bool cancelled, int skippedExists, int skippedChanged, int failed, ExitCode expected) // REQ-35
    {
        var result = new CopyResult
        {
            Started = DateTimeOffset.Now,
            Finished = DateTimeOffset.Now,
            SourceRoot = "a",
            DestinationRoot = "b",
            DryRun = false,
            Cancelled = cancelled,
            Summary = new CopySummary(5, 1, skippedExists, skippedChanged, failed, 0, 0),
            Entries = [],
        };

        Assert.Equal(expected, CopyCommand.ExitCodeFor(result));
    }

    private string ScanAndGetResultFile()
    {
        var scan = Cli.Scan(_folder, "--out", Path.Join(_folder.Root, "scans"));
        Assert.Equal(ExitCode.SuccessWithFindings, scan.Code);
        return Assert.Single(Directory.GetFiles(Path.Join(_folder.Root, "scans"), "MissingFiles_*.json"));
    }

    private CliRun Copy(string resultFile, params string[] extraArgs) =>
        Cli.Run(_folder, ["copy", "--result", resultFile, .. extraArgs]);
}
