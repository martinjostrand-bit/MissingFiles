using MissingFiles.Cli;
using MissingFiles.Core.Scanning;

namespace MissingFiles.FunctionalTests;

/// <summary>The <c>scan</c> command (spec REQ-33, REQ-33a, REQ-35).</summary>
public sealed class ScanCommandTests : IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void NothingMissingReturnsSuccessAndWritesResultFile()
    {
        _folder.AddBoth(@"2021\IMG_0001.JPG");

        var run = Cli.Scan(_folder);

        Assert.Equal(ExitCode.Success, run.Code);
        var resultFile = Assert.Single(_folder.OutputFiles("MissingFiles_*.json"));
        Assert.Contains($"Result file:", run.Out, StringComparison.Ordinal);
        Assert.Contains(resultFile, run.Out, StringComparison.Ordinal);
        Assert.Empty(ScanResultFile.Load(resultFile).MissingFiles);
    }

    [Fact]
    public void MissingFilesReturnFindingsAndAreListedInResultFile()
    {
        _folder.AddBoth("IMG_0001.JPG");
        _folder.AddSource(@"2021\IMG_0002.JPG");
        _folder.AddSource("IMG_0003.JPG", size: 20);
        _folder.AddDestination(@"other\IMG_0003.JPG", size: 21);

        var run = Cli.Scan(_folder);

        Assert.Equal(ExitCode.SuccessWithFindings, run.Code);
        Assert.Matches(@"Missing:\s+2", run.Out);
        Assert.Matches(@"same name, other size:\s+1", run.Out);
        var result = ScanResultFile.Load(Assert.Single(_folder.OutputFiles("*.json")));
        Assert.Equal([@"2021\IMG_0002.JPG", "IMG_0003.JPG"], result.MissingFiles.Select(m => m.RelativePath));
    }

    [Fact]
    public void OutOptionChoosesTheResultFolder()
    {
        var custom = Path.Join(_folder.Root, "custom out");

        var run = Cli.Scan(_folder, "--out", custom);

        Assert.Equal(ExitCode.Success, run.Code);
        Assert.Single(Directory.GetFiles(custom, "MissingFiles_*.json"));
        Assert.Empty(_folder.OutputFiles("*"));
    }

    [Fact]
    public void EqualsSyntaxIsAccepted()
    {
        var run = Cli.Run(_folder, ["scan", $"--source={_folder.Source}", $"--dest={_folder.Destination}"]);

        Assert.Equal(ExitCode.Success, run.Code);
    }

    [Fact]
    public void WithoutTypesFileTheBuiltInDefaultIsUsed() // REQ-33a
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("report.pdf");

        var run = Cli.Scan(_folder);

        Assert.Contains("built-in default", run.Out, StringComparison.Ordinal);
        var result = ScanResultFile.Load(Assert.Single(_folder.OutputFiles("*.json")));
        Assert.Null(result.FileTypesFilePath);
        Assert.Equal("IMG_0001.JPG", Assert.Single(result.MissingFiles).RelativePath);
    }

    [Fact]
    public void WithoutTypesFileTheUserFileIsUsedIfItExists() // REQ-33a
    {
        _folder.WriteFile(Path.GetRelativePath(_folder.Root, _folder.UserFileTypes), PdfOnly);
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("report.pdf");

        Cli.Scan(_folder);

        var result = ScanResultFile.Load(Assert.Single(_folder.OutputFiles("*.json")));
        Assert.Equal(_folder.UserFileTypes, result.FileTypesFilePath);
        Assert.Equal("report.pdf", Assert.Single(result.MissingFiles).RelativePath);
    }

    [Fact]
    public void TypesFileOptionIsUsedAndRecorded() // TC-18
    {
        var typesFile = _folder.WriteFile("types.json", PdfOnly);
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("report.pdf");

        var run = Cli.Scan(_folder, "--types-file", typesFile);

        Assert.Equal(ExitCode.SuccessWithFindings, run.Code);
        var result = ScanResultFile.Load(Assert.Single(_folder.OutputFiles("*.json")));
        Assert.Equal(typesFile, result.FileTypesFilePath);
        Assert.Equal([".pdf"], result.Extensions);
    }

    [Fact]
    public void GroupsOptionOverridesEnabledGroups() // TC-22
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("report.pdf");

        var run = Cli.Scan(_folder, "--groups", "documents");

        Assert.Equal(ExitCode.SuccessWithFindings, run.Code);
        var result = ScanResultFile.Load(Assert.Single(_folder.OutputFiles("*.json")));
        Assert.Equal("report.pdf", Assert.Single(result.MissingFiles).RelativePath);
    }

    [Theory]
    [InlineData("--groups", "Music", "Unknown group(s): Music")]
    [InlineData("--groups", " , ", "Option --groups needs at least one group name")]
    public void InvalidGroupsAreRejected(string option, string value, string expected) // REQ-33a
    {
        var run = Cli.Scan(_folder, option, value);

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains(expected, run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidTypesFileIsRejectedWithLineNumber() // TC-23
    {
        var typesFile = _folder.WriteFile("types.json", "{\n  \"schemaVersion\": 1,\n  oops\n}");

        var run = Cli.Scan(_folder, "--types-file", typesFile);

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains(typesFile, run.Error, StringComparison.Ordinal);
        Assert.Contains("line 3", run.Error, StringComparison.Ordinal);
        Assert.Empty(_folder.OutputFiles("*"));
    }

    [Fact]
    public void MissingTypesFileIsRejected() // TC-23
    {
        var run = Cli.Scan(_folder, "--types-file", Path.Join(_folder.Root, "nope.json"));

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains("could not be read", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidFoldersAreRejected() // TC-14
    {
        var inside = Directory.CreateDirectory(Path.Join(_folder.Source, "inner")).FullName;

        var run = Cli.Run(_folder, ["scan", "--source", _folder.Source, "--dest", inside]);

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains("destination folder is inside the source folder", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelledScanWritesNoResultFile() // REQ-15, REQ-34
    {
        _folder.AddSource("IMG_0001.JPG");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var run = Cli.Run(_folder, ["scan", "--source", _folder.Source, "--dest", _folder.Destination], cancellationToken: cts.Token);

        Assert.Equal(ExitCode.Cancelled, run.Code);
        Assert.Contains("No result file was written", run.Error, StringComparison.Ordinal);
        Assert.Empty(_folder.OutputFiles("*"));
    }

    [Theory]
    [InlineData(0, 0, ExitCode.Success)]
    [InlineData(0, 3, ExitCode.SuccessWithFindings)]
    [InlineData(1, 0, ExitCode.CompletedWithErrors)]
    [InlineData(1, 3, ExitCode.CompletedWithErrors)]
    public void ExitCodeFollowsSpecification(int errors, int missing, ExitCode expected) // REQ-35
    {
        var summary = new ScanSummary(10, 10 - missing, missing, errors, 0, 0);

        Assert.Equal(expected, ScanCommand.ExitCodeFor(summary));
    }

    [Fact]
    public void InteractiveProgressUsesOneUpdatingLine() // REQ-34
    {
        // Enough files for several progress updates (one per 256 files with the stepping clock),
        // since redirected output prints at most one progress line per 5 seconds.
        for (var i = 0; i < 1600; i++)
        {
            _folder.AddSource($"IMG_{i:D4}.JPG", size: 1);
        }

        string[] args = ["scan", "--source", _folder.Source, "--dest", _folder.Destination];

        // With a console window the progress line is rewritten with '\r'; redirected output
        // gets plain progress lines instead.
        var interactive = Cli.Run(_folder, args, interactive: true, timeProvider: new SteppingTimeProvider());
        var redirected = Cli.Run(_folder, args, interactive: false, timeProvider: new SteppingTimeProvider());

        Assert.Equal(ExitCode.SuccessWithFindings, interactive.Code);
        Assert.Contains("\rScanning source: 256 files", interactive.Out, StringComparison.Ordinal);
        var redirectedOut = redirected.Out.Replace(Environment.NewLine, "\n", StringComparison.Ordinal);
        Assert.DoesNotContain("\r", redirectedOut, StringComparison.Ordinal);
        Assert.Contains("\nScanning source: ", redirectedOut, StringComparison.Ordinal);
    }

    private const string PdfOnly = """
        { "schemaVersion": 1, "groups": [ { "name": "Docs", "extensions": [".pdf"] } ] }
        """;
}
