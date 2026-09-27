using MissingFiles.Core.Copying;

namespace MissingFiles.Core.Tests.Copying;

public sealed class CopyLogFileTests : IDisposable
{
    private static readonly DateTimeOffset Started = new(2026, 9, 27, 14, 3, 12, TimeSpan.FromHours(2));

    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void FileIsNamedAfterCopyStartAndNeverOverwritten()
    {
        var folder = Path.Join(_folder.Root, "out");

        var first = CopyLogFile.Save(SampleResult(), folder);
        var second = CopyLogFile.Save(SampleResult(), folder);

        Assert.Equal("MissingFilesCopy_20260927_140312.log", Path.GetFileName(first));
        Assert.Equal("MissingFilesCopy_20260927_140312_2.log", Path.GetFileName(second));
    }

    [Fact]
    public void LogHasHeaderSummaryAndDetailsInThatOrder() // REQ-32
    {
        var text = File.ReadAllText(CopyLogFile.Save(SampleResult(), _folder.Root));

        var header = text.IndexOf("[Header]", StringComparison.Ordinal);
        var summary = text.IndexOf("[Summary]", StringComparison.Ordinal);
        var details = text.IndexOf("[Details]", StringComparison.Ordinal);
        Assert.True(header >= 0 && header < summary && summary < details);

        Assert.Contains("Started:                   2026-09-27 14:03:12 +02:00", text, StringComparison.Ordinal);
        Assert.Contains(@"Scan result file:          C:\scans\MissingFiles_20260927_120000.json", text, StringComparison.Ordinal);
        Assert.Contains(@"Source folder:             D:\CameraBackup", text, StringComparison.Ordinal);
        Assert.Contains(@"Destination folder:        E:\PhotoArchive", text, StringComparison.Ordinal);
        Assert.Contains("Machine:                   " + Environment.MachineName, text, StringComparison.Ordinal);

        Assert.Contains("Files in scan result:      5", text, StringComparison.Ordinal);
        Assert.Contains("Copied:                    1", text, StringComparison.Ordinal);
        Assert.Contains("Skipped - exists:          1", text, StringComparison.Ordinal);
        Assert.Contains("Skipped - source changed:  1", text, StringComparison.Ordinal);
        Assert.Contains("Failed:                    1", text, StringComparison.Ordinal);
        Assert.Contains("Not processed:             1", text, StringComparison.Ordinal);
        Assert.Contains("Cancelled:                 Yes", text, StringComparison.Ordinal);
        Assert.Contains("Bytes copied:              4839211 (4.6 MB)", text, StringComparison.Ordinal);
        Assert.Contains("Elapsed:                   00:01:28", text, StringComparison.Ordinal);

        Assert.Contains(
            @"2026-09-27 14:03:12 | Copied                   | 2021\Bjørn æøå.JPG | 4839211 | ",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            @"2026-09-27 14:03:12 | Failed                   | 2021\locked.JPG | 5 | Access denied",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DryRunIsClearlyMarked()
    {
        var result = SampleResult(dryRun: true);

        var text = CopyLogFile.Format(result);

        Assert.StartsWith("MissingFiles copy log (DRY RUN - no files were written)", text, StringComparison.Ordinal);
        Assert.Contains("Mode:                      Dry run", text, StringComparison.Ordinal);
        Assert.Contains("Would copy:                1", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CopyStatus.Copied, "Copied")]
    [InlineData(CopyStatus.WouldCopy, "Would copy")]
    [InlineData(CopyStatus.SkippedExists, "Skipped - exists")]
    [InlineData(CopyStatus.SkippedSourceChanged, "Skipped - source changed")]
    [InlineData(CopyStatus.Failed, "Failed")]
    public void StatusTextIsReadable(CopyStatus status, string expected)
    {
        Assert.Equal(expected, CopyLogFile.StatusText(status));
    }

    private static CopyResult SampleResult(bool dryRun = false) => new()
    {
        Started = Started,
        Finished = Started.AddSeconds(88),
        ScanResultFilePath = @"C:\scans\MissingFiles_20260927_120000.json",
        SourceRoot = @"D:\CameraBackup",
        DestinationRoot = @"E:\PhotoArchive",
        DryRun = dryRun,
        Cancelled = true,
        Summary = new CopySummary(5, 1, 1, 1, 1, 1, 4839211),
        Entries =
        [
            new CopyEntry(Started, dryRun ? CopyStatus.WouldCopy : CopyStatus.Copied, @"2021\Bjørn æøå.JPG", 4839211, null),
            new CopyEntry(Started, CopyStatus.SkippedExists, @"2021\IMG_2.JPG", 10, null),
            new CopyEntry(Started, CopyStatus.SkippedSourceChanged, @"2021\IMG_3.JPG", 10, "The source file no longer exists."),
            new CopyEntry(Started, CopyStatus.Failed, @"2021\locked.JPG", 5, "Access denied"),
        ],
    };
}
