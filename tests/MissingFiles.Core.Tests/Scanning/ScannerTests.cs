using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

using MissingFiles.Core.FileTypes;
using MissingFiles.Core.Scanning;

namespace MissingFiles.Core.Tests.Scanning;

/// <summary>
/// Scan behaviour, following the test cases of spec §5.2 where they apply to the scan.
/// </summary>
public sealed class ScannerTests : IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void AllFilesMatchWithSameRelativePaths() // TC-01
    {
        _folder.AddBoth("IMG_0001.JPG");
        _folder.AddBoth(@"2021\Summer\VID_20210714_102205.mp4", size: 20);

        var result = Scan();

        Assert.Equal(new ScanSummary(2, 2, 0, 0, 0, 0), result.Summary);
        Assert.Empty(result.MissingFiles);
    }

    [Fact]
    public void FilesMatchInDifferentSubfolders() // TC-02
    {
        _folder.AddSource(@"Camera\IMG_0001.JPG");
        _folder.AddDestination(@"Archive\2021\07\IMG_0001.JPG");

        Assert.Equal(1, Scan().Summary.Matched);
    }

    [Fact]
    public void NoFilesMatch() // TC-03
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource(@"sub\IMG_0002.JPG");
        _folder.AddDestination("IMG_9999.JPG");

        var result = Scan();

        Assert.Equal(new ScanSummary(2, 0, 2, 0, 0, 0), result.Summary);
    }

    [Fact]
    public void ExactlyTheMissingFilesAreListedSortedWithDetails() // TC-04
    {
        _folder.AddBoth("IMG_0001.JPG");
        var missingPath = _folder.AddSource(@"b\IMG_0003.JPG", size: 33);
        _folder.AddSource(@"a\IMG_0002.JPG", size: 22);
        var lastWrite = new DateTime(2021, 7, 14, 10, 22, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(missingPath, lastWrite);

        var result = Scan();

        Assert.Equal([@"a\IMG_0002.JPG", @"b\IMG_0003.JPG"], result.MissingFiles.Select(m => m.RelativePath));
        Assert.Equal(new MissingFile(@"b\IMG_0003.JPG", 33, lastWrite, false), result.MissingFiles[1]);
        Assert.Equal(DateTimeKind.Utc, result.MissingFiles[1].LastWriteTimeUtc.Kind);
    }

    [Fact]
    public void DuplicateNamesInDestinationMatchIfOneHasTheSameSize() // TC-05
    {
        _folder.AddSource("IMG_0001.JPG", size: 50);
        _folder.AddDestination(@"x\IMG_0001.JPG", size: 10);
        _folder.AddDestination(@"y\IMG_0001.JPG", size: 50);

        Assert.Equal(1, Scan().Summary.Matched);
    }

    [Fact]
    public void DuplicateNamesInSourceAreEvaluatedIndividually() // TC-06
    {
        _folder.AddSource(@"2019\IMG_0001.JPG", size: 10);
        _folder.AddSource(@"2021\IMG_0001.JPG", size: 20);
        _folder.AddSource(@"2021\IMG_0002.JPG");
        _folder.AddSource(@"2022\img_0002.jpg");
        _folder.AddDestination("IMG_0001.JPG", size: 10);

        var result = Scan();

        Assert.Equal(1, result.Summary.Matched);
        Assert.Equal(3, result.Summary.Missing);
        Assert.Equal(2, result.Summary.DuplicateNameGroups);
        Assert.Contains(result.MissingFiles, m => m.RelativePath == @"2021\IMG_0001.JPG" && m.SameNameDifferentSize);
    }

    [Fact]
    public void NamesAreComparedCaseInsensitively() // TC-07
    {
        _folder.AddSource("img_1.jpg");
        _folder.AddDestination("IMG_1.JPG");

        Assert.Equal(1, Scan().Summary.Matched);
    }

    [Fact]
    public void JpgAndJpegAreDifferentFiles() // TC-08
    {
        _folder.AddSource("IMG_1.jpg");
        _folder.AddDestination("IMG_1.jpeg");

        var result = Scan();

        Assert.Equal(1, result.Summary.Missing);
        Assert.False(result.MissingFiles[0].SameNameDifferentSize);
    }

    [Fact]
    public void UnselectedTypesAreIgnored() // TC-09, TC-18
    {
        _folder.AddSource("notes.txt");
        _folder.AddSource("letter.docx");
        _folder.AddSource("IMG_1.jpg");

        var result = Scan();

        Assert.Equal(1, result.Summary.SourceFilesScanned);
        Assert.Equal(@"IMG_1.jpg", Assert.Single(result.MissingFiles).RelativePath);
    }

    [Fact]
    public void EmptyFoldersGiveEmptyResult() // TC-10
    {
        Directory.CreateDirectory(Path.Join(_folder.Source, "empty", "nested"));

        var result = Scan();

        Assert.Equal(new ScanSummary(0, 0, 0, 0, 0, 0), result.Summary);
    }

    [Fact]
    public void PathsLongerThan260CharactersAreHandled() // TC-11
    {
        var deep = string.Join('\\', Enumerable.Repeat("a_rather_long_folder_name_for_testing", 8));
        var relative = Path.Join(deep, "IMG_0001.JPG");
        _folder.AddSource(relative);
        Assert.True(Path.Join(_folder.Source, relative).Length > 260);

        var result = Scan();

        Assert.Equal(relative, Assert.Single(result.MissingFiles).RelativePath);
    }

    [Fact]
    public void UnreadableFolderIsReportedAndScanContinues() // TC-12
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource(@"locked\IMG_0002.JPG");
        var locked = new DirectoryInfo(Path.Join(_folder.Source, "locked"));
        var user = WindowsIdentity.GetCurrent().User!;
        var deny = new FileSystemAccessRule(user, FileSystemRights.ListDirectory, AccessControlType.Deny);
        var security = locked.GetAccessControl();
        security.AddAccessRule(deny);
        locked.SetAccessControl(security);

        try
        {
            var result = Scan();

            Assert.Equal(1, result.Summary.SourceFilesScanned);
            var error = Assert.Single(result.Errors);
            Assert.Equal(locked.FullName, error.Path);
            Assert.Equal(1, result.Summary.Errors);
        }
        finally
        {
            security.RemoveAccessRule(deny);
            locked.SetAccessControl(security);
        }
    }

    [Fact]
    public void SameNameWithDifferentSizeIsMissingAndFlagged() // TC-13
    {
        _folder.AddSource("IMG_0001.JPG", size: 100);
        _folder.AddDestination("IMG_0001.JPG", size: 99);

        var result = Scan();

        Assert.True(Assert.Single(result.MissingFiles).SameNameDifferentSize);
        Assert.Equal(1, result.Summary.SameNameDifferentSize);
    }

    [Fact]
    public void ZeroByteFilesMatch() // TC-16
    {
        _folder.AddBoth("empty.jpg", size: 0);

        Assert.Equal(1, Scan().Summary.Matched);
    }

    [Fact]
    public void SystemFoldersHiddenFilesAndHiddenFoldersAreSkipped() // TC-17, REQ-07
    {
        _folder.AddSource(@"$Recycle.Bin\S-1-5-21\IMG_0001.JPG");
        _folder.AddSource(@"System Volume Information\IMG_0002.JPG");
        var hiddenFile = _folder.AddSource("IMG_0003.JPG");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);
        _folder.AddSource(@"hidden\IMG_0004.JPG");
        File.SetAttributes(Path.Join(_folder.Source, "hidden"), FileAttributes.Directory | FileAttributes.Hidden);
        _folder.AddSource("IMG_0005.JPG");

        var result = Scan();

        Assert.Equal("IMG_0005.JPG", Assert.Single(result.MissingFiles).RelativePath);
    }

    [Fact]
    public void JunctionsAreNotFollowed() // REQ-07
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _folder.AddSource(@"photos\IMG_0001.JPG");
        var junction = Path.Join(_folder.Source, "photos", "loop");
        RunCmd($"mklink /J \"{junction}\" \"{_folder.Source}\"");

        try
        {
            Assert.True(Directory.Exists(Path.Join(junction, "photos")));

            Assert.Equal(1, Scan().Summary.SourceFilesScanned);
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    [Fact]
    public void AllFilesFilterIncludesFilesWithoutExtension() // TC-19
    {
        _folder.AddSource("README");
        _folder.AddSource("notes.txt");

        var result = Scan(new ExtensionFilter(["*"]));

        Assert.Equal(2, result.Summary.Missing);
        Assert.Equal(["*"], result.Extensions);
    }

    [Fact]
    public void EmptyExtensionFilterIncludesOnlyFilesWithoutExtension() // TC-20
    {
        _folder.AddSource("README");
        _folder.AddSource("notes.txt");

        var result = Scan(new ExtensionFilter([""]));

        Assert.Equal("README", Assert.Single(result.MissingFiles).RelativePath);
    }

    [Theory]
    [InlineData("same")]
    [InlineData("destination inside source")]
    [InlineData("source inside destination")]
    [InlineData("missing source")]
    [InlineData("missing destination")]
    [InlineData("empty source")]
    public void InvalidFoldersAreRejected(string scenario) // TC-14, REQ-02
    {
        var inside = Directory.CreateDirectory(Path.Join(_folder.Source, "inner")).FullName;
        var missing = Path.Join(_folder.Root, "does-not-exist");
        var (source, destination, message) = scenario switch
        {
            "same" => (_folder.Source, _folder.Source + @"\", "same folder"),
            "destination inside source" => (_folder.Source, inside, "destination folder is inside the source"),
            "source inside destination" => (inside, _folder.Source.ToUpperInvariant(), "source folder is inside the destination"),
            "missing source" => (missing, _folder.Destination, "Source folder does not exist"),
            "missing destination" => (_folder.Source, missing, "Destination folder does not exist"),
            _ => ("  ", _folder.Destination, "Source folder is not specified"),
        };

        var ex = Assert.Throws<ScanValidationException>(() => Scanner.Run(Options(source, destination)));

        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SiblingFolderWithCommonPrefixIsNotNested()
    {
        var a = Directory.CreateDirectory(Path.Join(_folder.Root, "photos")).FullName;
        var b = Directory.CreateDirectory(Path.Join(_folder.Root, "photos2")).FullName;

        var result = Scanner.Run(Options(a, b));

        Assert.Equal(a, result.SourceRoot);
    }

    [Fact]
    public void ResultRecordsSettingsAndTimes()
    {
        var now = new DateTimeOffset(2026, 9, 27, 14, 3, 12, TimeSpan.FromHours(2));
        var options = new ScanOptions
        {
            SourceRoot = _folder.Source + @"\",
            DestinationRoot = _folder.Destination,
            Filter = new ExtensionFilter([".mp4", ".jpg"]),
            FileTypesFilePath = @"C:\types.json",
            TimeProvider = new FixedTimeProvider(now),
        };

        var result = Scanner.Run(options);

        Assert.Equal(_folder.Source, result.SourceRoot);
        Assert.Equal(_folder.Destination, result.DestinationRoot);
        Assert.Equal(@"C:\types.json", result.FileTypesFilePath);
        Assert.Equal([".jpg", ".mp4"], result.Extensions);
        Assert.Equal(ScanResult.NameAndSizeMatchMode, result.MatchMode);
        Assert.Equal(now, result.ScanStarted);
        Assert.Equal(now.Offset, result.ScanStarted.Offset);
        Assert.Equal(ProductInfo.DisplayName, result.Application);
    }

    [Fact]
    public void ProgressEndsWithCompletedReport()
    {
        for (var i = 0; i < 600; i++)
        {
            _folder.AddBoth($"IMG_{i:D4}.JPG");
        }

        _folder.AddSource("IMG_9999.JPG");
        var progress = new ProgressRecorder<ScanProgress>();

        Scanner.Run(Options(), progress);

        var last = progress.Reports[^1];
        Assert.Equal(ScanPhase.Completed, last.Phase);
        Assert.Equal(600, last.DestinationFilesIndexed);
        Assert.Equal(601, last.SourceFilesScanned);
        Assert.Equal(600, last.Matched);
        Assert.Equal(1, last.Missing);
    }

    [Fact]
    public void CancelledScanThrows() // REQ-15
    {
        _folder.AddSource("IMG_0001.JPG");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => Scanner.Run(Options(), cancellationToken: cts.Token));
    }

    private ScanResult Scan(ExtensionFilter? filter = null) =>
        Scanner.Run(Options(filter: filter));

    private ScanOptions Options(string? source = null, string? destination = null, ExtensionFilter? filter = null) => new()
    {
        SourceRoot = source ?? _folder.Source,
        DestinationRoot = destination ?? _folder.Destination,
        Filter = filter ?? FileTypesFile.LoadDefault().CreateFilter(),
    };

    private static void RunCmd(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c {arguments}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
