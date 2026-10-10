using MissingFiles.Cli;
using MissingFiles.Core.Scanning;

namespace MissingFiles.FunctionalTests;

/// <summary>Folders to scan, after <see cref="ScanCase.Prepare"/>, and what to undo afterwards.</summary>
internal sealed record PreparedCase(string Source, string Destination, IDisposable? Cleanup = null);

/// <summary>
/// One test case of spec §5.2: the folder content and the expected result (counts and listed files).
/// </summary>
internal sealed class ScanCase
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>Creates the files of the case in the test folder.</summary>
    public required Action<TestFolder> Arrange { get; init; }

    /// <summary>Sets up conditions such as a locked folder or a drive root, and the folders to scan.</summary>
    public Func<TestFolder, PreparedCase> Prepare { get; init; } = f => new(f.Source, f.Destination);

    /// <summary>Extra scan options, e.g. <c>--types-file</c> or <c>--groups</c>.</summary>
    public Func<TestFolder, string[]> Options { get; init; } = _ => [];

    /// <summary>For a scan that must not start: text expected on standard error (exit code 3).</summary>
    public string? RejectedWith { get; init; }

    public ScanSummary? Summary { get; init; }

    /// <summary>Relative paths of the missing files.</summary>
    public string[] Missing { get; init; } = [];

    /// <summary>Missing files flagged <c>sameNameDifferentSize</c>.</summary>
    public string[] Flagged { get; init; } = [];

    /// <summary>Expected <c>extensions</c> in the result file, or null to not check them.</summary>
    public string[]? Extensions { get; init; }

    /// <summary>Folders that cannot be read, relative to the source.</summary>
    public string[] ErrorFolders { get; init; } = [];

    /// <summary>Missing files that the copy skips by design because the target path exists (TC-15).</summary>
    public string[] SkippedByCopy { get; init; } = [];

    public bool IsRejected => RejectedWith is not null;

    public ExitCode ExpectedExitCode =>
        IsRejected ? ExitCode.InvalidArguments
        : ErrorFolders.Length > 0 ? ExitCode.CompletedWithErrors
        : Missing.Length > 0 ? ExitCode.SuccessWithFindings
        : ExitCode.Success;

    public override string ToString() => $"{Id} {Title}";
}

/// <summary>The test cases TC-01..TC-23 of spec §5.2.</summary>
internal static class ScanCases
{
    private const string PdfOnly = """{ "schemaVersion": 1, "groups": [ { "name": "Documents", "extensions": [".pdf"] } ] }""";

    private static readonly string LongFolder = string.Join('\\', Enumerable.Repeat("A folder with a rather long name for testing", 7));

    // Generated files (FT-01) for the cases with many files.
    private static readonly IReadOnlyList<string> Twenty = TestData.RelativePaths(20);
    private static readonly IReadOnlyList<string> Ten = TestData.RelativePaths(10);
    private static readonly IReadOnlyList<string> TenOthers = TestData.RelativePaths(10, first: 501);
    private static readonly IReadOnlyList<string> Twelve = TestData.RelativePaths(12);
    private static readonly string[] TwelveMissing = [Twelve[2], Twelve[6], Twelve[10]];

    public static IReadOnlyList<ScanCase> All { get; } =
    [
        new()
        {
            Id = "TC-01",
            Title = "All files match, same relative paths",
            Arrange = f => AddGenerated(f, Twenty, p => p),
            Summary = new(20, 20, 0, 0, 0, 0),
        },
        new()
        {
            Id = "TC-02",
            Title = "All files match, other subfolders in the destination",
            Arrange = f => AddGenerated(f, Twenty, p => Path.Join("Archive", "Imported 2024", Path.GetFileName(p))),
            Summary = new(20, 20, 0, 0, 0, 0),
        },
        new()
        {
            Id = "TC-03",
            Title = "No files match",
            Arrange = f =>
            {
                f.AddSourceFiles(Ten);
                foreach (var path in TenOthers)
                {
                    f.AddDestination(path, TestData.SizeOf(path));
                }
            },
            Summary = new(10, 0, 10, 0, 0, 0),
            Missing = [.. Ten],
        },
        new()
        {
            Id = "TC-04",
            Title = "Some files missing",
            Arrange = f => AddGenerated(f, Twelve, p => TwelveMissing.Contains(p) ? null : p),
            Summary = new(12, 9, 3, 0, 0, 0),
            Missing = TwelveMissing,
        },
        new()
        {
            Id = "TC-05",
            Title = "Duplicate names in the destination, one with matching size",
            Arrange = f =>
            {
                f.AddSource(@"DCIM\100CANON\IMG_0001.JPG", 700);
                f.AddDestination(@"2021\IMG_0001.JPG", 650);
                f.AddDestination(@"2022\IMG_0001.JPG", 700);
                f.AddDestination(@"2023\IMG_0001.JPG", 900);
            },
            Summary = new(1, 1, 0, 0, 0, 0),
        },
        new()
        {
            Id = "TC-06",
            Title = "Duplicate names in the source, in different folders",
            Arrange = f =>
            {
                f.AddSource(@"Card A\DCIM\IMG_0001.JPG", 500);
                f.AddSource(@"Card B\DCIM\IMG_0001.JPG", 500);
                f.AddSource(@"Card C\DCIM\IMG_0001.JPG", 800);
                f.AddSource(@"Card C\DCIM\IMG_0002.JPG", 300);
                f.AddDestination(@"Archive\IMG_0001.JPG", 500);
            },
            Summary = new(4, 2, 2, 0, 1, 1),
            Missing = [@"Card C\DCIM\IMG_0001.JPG", @"Card C\DCIM\IMG_0002.JPG"],
            Flagged = [@"Card C\DCIM\IMG_0001.JPG"],
        },
        new()
        {
            Id = "TC-07",
            Title = "Names differ only in case",
            Arrange = f =>
            {
                f.AddSource("img_1.jpg", 400);
                f.AddDestination("IMG_1.JPG", 400);
                f.AddSource(@"DCIM\Dsc04512.arw", 900);
                f.AddDestination(@"Raw\DSC04512.ARW", 900);
            },
            Summary = new(2, 2, 0, 0, 0, 0),
        },
        new()
        {
            Id = "TC-08",
            Title = ".jpg and .jpeg with the same base name",
            Arrange = f =>
            {
                f.AddSource("IMG_0001.jpg", 400);
                f.AddDestination("IMG_0001.jpeg", 400);
            },
            Summary = new(1, 0, 1, 0, 0, 0),
            Missing = ["IMG_0001.jpg"],
        },
        new()
        {
            Id = "TC-09",
            Title = "Unsupported types in the source are ignored",
            Arrange = f =>
            {
                f.AddSource("IMG_0001.JPG", 400);
                f.AddSource("notes.txt", 100);
                f.AddSource("Holiday plan.docx", 200);
                f.AddSource("Thumbs.db", 50);
            },
            Summary = new(1, 0, 1, 0, 0, 0),
            Missing = ["IMG_0001.JPG"],
        },
        new()
        {
            Id = "TC-10a",
            Title = "Empty source",
            Arrange = f => f.AddDestination(@"Archive\IMG_0001.JPG", 400),
            Summary = new(0, 0, 0, 0, 0, 0),
        },
        new()
        {
            Id = "TC-10b",
            Title = "Empty source and destination",
            Arrange = _ => { },
            Summary = new(0, 0, 0, 0, 0, 0),
        },
        new()
        {
            Id = "TC-11",
            Title = "Deeply nested folders, paths longer than 260 characters",
            Arrange = f =>
            {
                f.AddSource(Path.Join(LongFolder, "IMG_0001.JPG"), 400);
                f.AddSource(Path.Join(LongFolder, "VID_0002.MP4"), 900);
                f.AddDestination(Path.Join(LongFolder, "VID_0002.MP4"), 900);
                Assert.True(Path.Join(f.Source, LongFolder, "IMG_0001.JPG").Length > 300);
            },
            Summary = new(2, 1, 1, 0, 0, 0),
            Missing = [Path.Join(LongFolder, "IMG_0001.JPG")],
        },
        new()
        {
            Id = "TC-12",
            Title = "Unreadable folder is listed as an error, the scan completes",
            Arrange = f =>
            {
                f.AddSource("IMG_0001.JPG", 400);
                f.AddSource(@"Locked\IMG_0002.JPG", 400);
            },
            Prepare = f => new(f.Source, f.Destination, WindowsEnvironment.DenyListing(Path.Join(f.Source, "Locked"))),
            Summary = new(1, 0, 1, 1, 0, 0),
            Missing = ["IMG_0001.JPG"],
            ErrorFolders = ["Locked"],
        },
        new()
        {
            Id = "TC-13",
            Title = "Same name, different size",
            Arrange = f =>
            {
                f.AddSource(@"2021\IMG_0001.JPG", 400);
                f.AddDestination(@"Archive\IMG_0001.JPG", 399);
            },
            Summary = new(1, 0, 1, 0, 0, 1),
            Missing = [@"2021\IMG_0001.JPG"],
            Flagged = [@"2021\IMG_0001.JPG"],
        },
        new()
        {
            Id = "TC-14",
            Title = "Destination inside the source is rejected",
            Arrange = f => f.AddSource("IMG_0001.JPG", 400),
            Prepare = f => new(f.Source, Directory.CreateDirectory(Path.Join(f.Source, "Backup")).FullName),
            RejectedWith = "The destination folder is inside the source folder",
        },
        new()
        {
            Id = "TC-15",
            Title = "Same name, different size, same relative path; the copy skips it",
            Arrange = f =>
            {
                f.AddSource(@"2021\IMG_0001.JPG", 400);
                f.AddDestination(@"2021\IMG_0001.JPG", 300);
                f.AddSource(@"2021\IMG_0002.JPG", 400);
            },
            Summary = new(2, 0, 2, 0, 0, 1),
            Missing = [@"2021\IMG_0001.JPG", @"2021\IMG_0002.JPG"],
            Flagged = [@"2021\IMG_0001.JPG"],
            SkippedByCopy = [@"2021\IMG_0001.JPG"],
        },
        new()
        {
            Id = "TC-16",
            Title = "Zero-byte files with the same name",
            Arrange = f =>
            {
                f.AddSource("IMG_0001.JPG", 0);
                f.AddDestination(@"Archive\IMG_0001.JPG", 0);
                f.AddSource("IMG_0002.JPG", 0);
            },
            Summary = new(2, 1, 1, 0, 0, 0),
            Missing = ["IMG_0002.JPG"],
        },
        new()
        {
            Id = "TC-17",
            Title = "Source is a drive root with $Recycle.Bin and System Volume Information",
            Arrange = f =>
            {
                f.AddSource(@"$Recycle.Bin\S-1-5-21-1000\$RABC123.JPG", 400);
                f.AddSource(@"System Volume Information\IMG_0002.JPG", 400);
                f.AddSource(@"$WinREAgent\IMG_0003.JPG", 400);
                f.AddSource(@"DCIM\100CANON\IMG_0004.JPG", 400);
            },
            Prepare = f =>
            {
                var (root, cleanup) = WindowsEnvironment.Subst(f.Source);
                return new(root, f.Destination, cleanup);
            },
            Summary = new(1, 0, 1, 0, 0, 0),
            Missing = [@"DCIM\100CANON\IMG_0004.JPG"],
        },
        new()
        {
            Id = "TC-18",
            Title = "Custom file types file with only .pdf",
            Arrange = f =>
            {
                f.WriteFile("types.json", PdfOnly);
                f.AddSource("Report 2021.pdf", 400);
                f.AddSource("IMG_0001.JPG", 400);
            },
            Options = f => ["--types-file", Path.Join(f.Root, "types.json")],
            Summary = new(1, 0, 1, 0, 0, 0),
            Missing = ["Report 2021.pdf"],
            Extensions = [".pdf"],
        },
        new()
        {
            Id = "TC-19",
            Title = "File types file with \"*\": all files, also without extension",
            Arrange = f =>
            {
                f.WriteFile("types.json", """{ "schemaVersion": 1, "groups": [ { "name": "All", "extensions": ["*"] } ] }""");
                f.AddSource("IMG_0001.JPG", 400);
                f.AddSource("notes.txt", 100);
                f.AddSource("README", 50);
                f.AddSource("backup.tar.gz", 300);
                f.AddDestination(@"Docs\notes.txt", 100);
            },
            Options = f => ["--types-file", Path.Join(f.Root, "types.json")],
            Summary = new(4, 1, 3, 0, 0, 0),
            Missing = ["IMG_0001.JPG", "README", "backup.tar.gz"],
            Extensions = ["*"],
        },
        new()
        {
            Id = "TC-20",
            Title = "File types file with \"\": only files without extension",
            Arrange = f =>
            {
                f.WriteFile("types.json", """{ "schemaVersion": 1, "groups": [ { "name": "No extension", "extensions": [""] } ] }""");
                f.AddSource("README", 50);
                f.AddSource("Makefile", 60);
                f.AddSource("IMG_0001.JPG", 400);
                f.AddDestination(@"src\Makefile", 60);
            },
            Options = f => ["--types-file", Path.Join(f.Root, "types.json")],
            Summary = new(2, 1, 1, 0, 0, 0),
            Missing = ["README"],
            Extensions = [""],
        },
        new()
        {
            Id = "TC-21",
            Title = "Extensions written as JPG, jpg and .Jpg",
            Arrange = f =>
            {
                f.WriteFile("types.json", """
                    // Edited by hand: comments and trailing commas are allowed.
                    { "schemaVersion": 1, "groups": [ { "name": "Pictures", "extensions": ["JPG", "jpg", ".Jpg",], }, ], }
                    """);
                f.AddSource("IMG_0001.JPG", 400);
                f.AddSource("img_0002.jpg", 400);
                f.AddSource("IMG_0003.Jpg", 400);
                f.AddSource("IMG_0004.jpeg", 400);
            },
            Options = f => ["--types-file", Path.Join(f.Root, "types.json")],
            Summary = new(3, 0, 3, 0, 0, 0),
            Missing = ["IMG_0001.JPG", "IMG_0003.Jpg", "img_0002.jpg"],
            Extensions = [".jpg"],
        },
        new()
        {
            Id = "TC-22",
            Title = "Group disabled in the file, enabled with --groups",
            Arrange = f =>
            {
                f.WriteFile("types.json", """
                    { "schemaVersion": 1, "groups": [
                        { "name": "Pictures", "enabled": true, "extensions": [".jpg"] },
                        { "name": "Documents", "enabled": false, "extensions": [".pdf"] } ] }
                    """);
                f.AddSource("IMG_0001.jpg", 400);
                f.AddSource("Report.pdf", 400);
            },
            Options = f => ["--types-file", Path.Join(f.Root, "types.json"), "--groups", "Documents"],
            Summary = new(1, 0, 1, 0, 0, 0),
            Missing = ["Report.pdf"],
            Extensions = [".pdf"],
        },
        new()
        {
            Id = "TC-23a",
            Title = "File types file missing",
            Arrange = f => f.AddSource("IMG_0001.JPG", 400),
            Options = f => ["--types-file", Path.Join(f.Root, "types.json")],
            RejectedWith = "The file could not be read",
        },
        new()
        {
            Id = "TC-23b",
            Title = "File types file is not valid JSON",
            Arrange = f =>
            {
                f.WriteFile("types.json", "{\n  \"schemaVersion\": 1,\n  \"groups\": [ { \"name\": \"Pictures\" \"extensions\": [] } ]\n}");
                f.AddSource("IMG_0001.JPG", 400);
            },
            Options = f => ["--types-file", Path.Join(f.Root, "types.json")],
            RejectedWith = "line 3",
        },
        new()
        {
            Id = "TC-23c",
            Title = "File types file without enabled extensions",
            Arrange = f =>
            {
                f.WriteFile("types.json", """{ "schemaVersion": 1, "groups": [ { "name": "Pictures", "enabled": false, "extensions": [".jpg"] } ] }""");
                f.AddSource("IMG_0001.JPG", 400);
            },
            Options = f => ["--types-file", Path.Join(f.Root, "types.json")],
            RejectedWith = "No file types are enabled",
        },
    ];

    public static ScanCase Get(string id) => All.Single(c => c.Id == id);

    /// <summary>All case ids, for <c>[MemberData]</c>.</summary>
    public static TheoryData<string> Ids => [.. All.Select(c => c.Id)];

    /// <summary>Ids of the cases where a scan runs, for the round-trip test (FT-08).</summary>
    public static TheoryData<string> ScanningIds => [.. All.Where(c => !c.IsRejected).Select(c => c.Id)];

    /// <summary>Adds each generated file to the source and, where <paramref name="destinationPath"/> gives a path, to the destination.</summary>
    private static void AddGenerated(TestFolder folder, IEnumerable<string> paths, Func<string, string?> destinationPath)
    {
        foreach (var path in folder.AddSourceFiles(paths))
        {
            if (destinationPath(path) is { } target)
            {
                folder.AddDestination(target, TestData.SizeOf(path));
            }
        }
    }
}
