using MissingFiles.Core.Copying;
using MissingFiles.Core.FileTypes;
using MissingFiles.Core.Scanning;

namespace MissingFiles.Core.Tests.Copying;

/// <summary>
/// Copy behaviour, following spec §5.4 (FT-03..FT-07) and §5.5 (FT-08).
/// </summary>
public sealed class CopierTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 14, 3, 12, TimeSpan.FromHours(2));

    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void CopiesMissingFilesWithIdenticalContentAndTimestamp() // FT-03
    {
        var first = AddSourceWithContent(@"2021\Summer\IMG_0001.JPG", 1000, seed: 1);
        var second = AddSourceWithContent("VID_0001.MP4", 3 * 1024 * 1024 + 17, seed: 2); // larger than the copy buffer
        var lastWrite = new DateTime(2021, 7, 14, 10, 22, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first, lastWrite);

        var result = Copy(Scan());

        Assert.Equal(new CopySummary(2, 2, 0, 0, 0, 0, 1000 + (3 * 1024 * 1024) + 17), result.Summary);
        Assert.False(result.Cancelled);
        AssertSameFile(first, Path.Join(_folder.Destination, @"2021\Summer\IMG_0001.JPG"));
        AssertSameFile(second, Path.Join(_folder.Destination, "VID_0001.MP4"));
        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(Path.Join(_folder.Destination, @"2021\Summer\IMG_0001.JPG")));
        Assert.All(result.Entries, e => Assert.Equal(CopyStatus.Copied, e.Status));
        AssertNoTempFiles();
    }

    [Fact]
    public void ExistingDestinationFileIsSkippedAndNotOverwritten() // FT-04, TC-15
    {
        _folder.AddSource(@"a\IMG_0001.JPG", size: 100);
        _folder.AddDestination(@"a\IMG_0001.JPG", size: 50);

        var scan = Scan();
        Assert.True(Assert.Single(scan.MissingFiles).SameNameDifferentSize);
        var result = Copy(scan);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(CopyStatus.SkippedExists, entry.Status);
        Assert.Equal(50, new FileInfo(Path.Join(_folder.Destination, @"a\IMG_0001.JPG")).Length);
        Assert.Equal(1, result.Summary.SkippedExists);
    }

    [Fact]
    public void DestinationFileCreatedAfterScanIsNotOverwritten() // FT-04
    {
        _folder.AddSource("IMG_0001.JPG", size: 100);
        var scan = Scan();
        _folder.AddDestination("IMG_0001.JPG", size: 7);

        var result = Copy(scan);

        Assert.Equal(CopyStatus.SkippedExists, Assert.Single(result.Entries).Status);
        Assert.Equal(7, new FileInfo(Path.Join(_folder.Destination, "IMG_0001.JPG")).Length);
    }

    [Fact]
    public void DeletedSourceFileIsSkipped() // FT-05
    {
        var path = _folder.AddSource("IMG_0001.JPG");
        var scan = Scan();
        File.Delete(path);

        var entry = Assert.Single(Copy(scan).Entries);

        Assert.Equal(CopyStatus.SkippedSourceChanged, entry.Status);
        Assert.Contains("no longer exists", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceFileWithDifferentSizeIsSkipped() // FT-05
    {
        var path = _folder.AddSource("IMG_0001.JPG", size: 10);
        var scan = Scan();
        var lastWrite = File.GetLastWriteTimeUtc(path);
        File.WriteAllBytes(path, new byte[11]);
        File.SetLastWriteTimeUtc(path, lastWrite);

        var result = Copy(scan);

        Assert.Equal(CopyStatus.SkippedSourceChanged, Assert.Single(result.Entries).Status);
        Assert.False(File.Exists(Path.Join(_folder.Destination, "IMG_0001.JPG")));
    }

    [Fact]
    public void SourceFileWithDifferentTimestampIsSkipped() // FT-05
    {
        var path = _folder.AddSource("IMG_0001.JPG");
        var scan = Scan();
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));

        Assert.Equal(CopyStatus.SkippedSourceChanged, Assert.Single(Copy(scan).Entries).Status);
    }

    [Fact]
    public void CancelStopsAfterCurrentFileAndLeavesNoPartialFiles() // FT-06, REQ-30
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("IMG_0002.JPG");
        _folder.AddSource("IMG_0003.JPG");
        var scan = Scan();
        using var cts = new CancellationTokenSource();
        var progress = new ProgressRecorder<CopyProgress>(p =>
        {
            if (p.FilesProcessed >= 1)
            {
                cts.Cancel();
            }
        });

        var result = Copier.Run(Options(scan, new SteppingTimeProvider(Now)), progress, cts.Token);

        Assert.True(result.Cancelled);
        Assert.Equal(new CopySummary(3, 1, 0, 0, 0, 2, 10), result.Summary);
        Assert.Single(Directory.GetFiles(_folder.Destination, "*", SearchOption.AllDirectories));
        AssertNoTempFiles();
    }

    [Fact]
    public void CancelBeforeStartCopiesNothing()
    {
        _folder.AddSource("IMG_0001.JPG");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = Copier.Run(Options(Scan()), cancellationToken: cts.Token);

        Assert.True(result.Cancelled);
        Assert.Equal(1, result.Summary.NotProcessed);
        Assert.Empty(Directory.GetFiles(_folder.Destination));
    }

    [Fact]
    public void FailedFileIsLoggedAndCopyContinues() // REQ-29
    {
        var locked = _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("IMG_0002.JPG");
        var scan = Scan();

        CopyResult result;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = Copy(scan);
        }

        var failed = result.Entries.Single(e => e.RelativePath == "IMG_0001.JPG");
        Assert.Equal(CopyStatus.Failed, failed.Status);
        Assert.False(string.IsNullOrEmpty(failed.Message));
        Assert.Equal(CopyStatus.Copied, result.Entries.Single(e => e.RelativePath == "IMG_0002.JPG").Status);
        Assert.Equal(new CopySummary(2, 1, 0, 0, 1, 0, 10), result.Summary);
        Assert.False(File.Exists(Path.Join(_folder.Destination, "IMG_0001.JPG")));
        AssertNoTempFiles();
    }

    [Fact]
    public void DryRunWritesNothing() // FT-07
    {
        _folder.AddSource(@"sub\IMG_0001.JPG");
        _folder.AddSource("IMG_0002.JPG");

        var result = Copier.Run(Options(Scan(), dryRun: true));

        Assert.True(result.DryRun);
        Assert.All(result.Entries, e => Assert.Equal(CopyStatus.WouldCopy, e.Status));
        Assert.Equal(new CopySummary(2, 2, 0, 0, 0, 0, 0), result.Summary);
        Assert.Empty(Directory.GetFileSystemEntries(_folder.Destination));
    }

    [Theory]
    [InlineData(@"..\outside.jpg")]
    [InlineData(@"sub\..\..\outside.jpg")]
    [InlineData(@"C:\Windows\outside.jpg")]
    [InlineData(@"\outside.jpg")]
    [InlineData("")]
    public void RelativePathsOutsideTheRootsAreRejected(string relativePath)
    {
        TestFolder.CreateFile(_folder.Root, "outside.jpg");
        var scan = ScanResultWith(new MissingFile(relativePath, 10, DateTime.UtcNow, false));

        var entry = Assert.Single(Copy(scan).Entries);

        Assert.Equal(CopyStatus.Failed, entry.Status);
        Assert.Contains("Invalid relative path", entry.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(_folder.Destination));
    }

    [Fact]
    public void NotEnoughSpaceStopsCopyButNotDryRun() // REQ-23
    {
        _folder.AddSource("IMG_0001.JPG", size: 100);
        var scan = Scan();

        var ex = Assert.Throws<CopyValidationException>(() => Copier.Run(Options(scan, freeBytes: 99)));
        Assert.Contains("Not enough free space", ex.Message, StringComparison.Ordinal);

        var dryRun = Copier.Run(Options(scan, freeBytes: 99, dryRun: true));
        Assert.Equal(1, dryRun.Summary.Copied);
    }

    [Fact]
    public void PreviewShowsFolderCountSizeAndFreeSpace() // REQ-23
    {
        _folder.AddSource("IMG_0001.JPG", size: 100);
        _folder.AddSource("IMG_0002.JPG", size: 23);

        var preview = Copier.Preview(Scan());

        Assert.Equal(_folder.Source, preview.SourceRoot);
        Assert.Equal(_folder.Destination, preview.DestinationRoot);
        Assert.Equal(2, preview.FileCount);
        Assert.Equal(123, preview.TotalBytes);
        Assert.True(preview.FreeBytes > 0);
        Assert.True(preview.HasEnoughSpace);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(100L, true)]
    [InlineData(99L, false)]
    public void HasEnoughSpaceTreatsUnknownAsEnough(long? freeBytes, bool expected)
    {
        Assert.Equal(expected, new CopyPreview("a", "b", 1, 100, freeBytes).HasEnoughSpace);
    }

    [Fact]
    public void MissingDestinationFolderIsRejected()
    {
        var scan = Scan();
        Directory.Delete(_folder.Destination);

        var ex = Assert.Throws<CopyValidationException>(() => Copier.Preview(scan));

        Assert.Contains("Destination folder does not exist", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanCopyScanFindsNothingMissing() // FT-08
    {
        _folder.AddBoth(@"2020\IMG_0001.JPG");
        _folder.AddSource(@"2021\IMG_0002.JPG");
        _folder.AddSource(@"2021\deep\nested\VID_0001.MP4", size: 5000);
        _folder.AddSource(@"2022\IMG_0003.JPG", size: 100);
        _folder.AddDestination(@"2022\IMG_0003.JPG", size: 99); // TC-15: skipped by design

        var first = Scan();
        var copy = Copy(first);
        var second = Scan();

        Assert.Equal(3, first.Summary.Missing);
        Assert.Equal(2, copy.Summary.Copied);
        Assert.Equal(1, copy.Summary.SkippedExists);
        Assert.Equal(@"2022\IMG_0003.JPG", Assert.Single(second.MissingFiles).RelativePath);
    }

    [Fact]
    public void ProgressReportsBytesEstimateAndCompletion() // REQ-30
    {
        _folder.AddSource("IMG_0001.JPG", size: 1000);
        _folder.AddSource("IMG_0002.JPG", size: 3000);
        var progress = new ProgressRecorder<CopyProgress>();

        Copier.Run(Options(Scan(), new SteppingTimeProvider(Now)), progress);

        Assert.Contains(progress.Reports, p => p.CurrentFile == "IMG_0001.JPG");
        Assert.Contains(progress.Reports, p => p.EstimatedRemaining > TimeSpan.Zero);
        var last = progress.Reports[^1];
        Assert.Equal(2, last.FilesProcessed);
        Assert.Equal(2, last.TotalFiles);
        Assert.Equal(2, last.Copied);
        Assert.Equal(4000, last.BytesCopied);
        Assert.Equal(4000, last.TotalBytes);
        Assert.Equal(TimeSpan.Zero, last.EstimatedRemaining);
        Assert.Null(last.CurrentFile);
    }

    [Fact]
    public void ResultRecordsSettingsAndTimes()
    {
        _folder.AddSource("IMG_0001.JPG");

        var result = Copier.Run(Options(Scan(), new FixedTimeProvider(Now)));

        Assert.Equal(Now, result.Started);
        Assert.Equal(@"C:\scans\result.json", result.ScanResultFilePath);
        Assert.Equal(_folder.Source, result.SourceRoot);
        Assert.Equal(_folder.Destination, result.DestinationRoot);
        Assert.Equal(Now, Assert.Single(result.Entries).Time);
    }

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1023, "1023 bytes")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5L * 1024 * 1024 * 1024, "5.0 GB")]
    public void FormatBytesUsesReadableUnits(long bytes, string expected)
    {
        Assert.Equal(expected, Copier.FormatBytes(bytes));
    }

    private string AddSourceWithContent(string relativePath, int size, int seed)
    {
        var path = _folder.AddSource(relativePath, 0);
        var content = new byte[size];
        new Random(seed).NextBytes(content);
        File.WriteAllBytes(path, content);
        return path;
    }

    private ScanResult Scan() => Scanner.Run(new ScanOptions
    {
        SourceRoot = _folder.Source,
        DestinationRoot = _folder.Destination,
        Filter = new ExtensionFilter(["*"]),
    });

    private ScanResult ScanResultWith(params MissingFile[] files) => new()
    {
        ScanStarted = Now,
        ScanFinished = Now,
        SourceRoot = _folder.Source,
        DestinationRoot = _folder.Destination,
        Extensions = ["*"],
        Summary = new ScanSummary(files.Length, 0, files.Length, 0, 0, 0),
        MissingFiles = files,
        Errors = [],
    };

    private static CopyResult Copy(ScanResult scan) => Copier.Run(Options(scan));

    private static CopyOptions Options(
        ScanResult scan, TimeProvider? time = null, long? freeBytes = long.MaxValue, bool dryRun = false) => new()
        {
            ScanResult = scan,
            ScanResultFilePath = @"C:\scans\result.json",
            DryRun = dryRun,
            TimeProvider = time ?? TimeProvider.System,
            FreeSpaceProvider = _ => freeBytes,
        };

    private static void AssertSameFile(string expected, string actual)
    {
        Assert.Equal(File.ReadAllBytes(expected), File.ReadAllBytes(actual));
    }

    private void AssertNoTempFiles()
    {
        Assert.Empty(Directory.GetFiles(_folder.Destination, "*" + Copier.TempSuffix, SearchOption.AllDirectories));
    }
}
