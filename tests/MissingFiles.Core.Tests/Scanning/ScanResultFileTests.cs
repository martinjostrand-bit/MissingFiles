using System.Text.Json;

using MissingFiles.Core.Scanning;

namespace MissingFiles.Core.Tests.Scanning;

public sealed class ScanResultFileTests : IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void FileIsNamedAfterScanStartInLocalTime() // REQ-18
    {
        var path = ScanResultFile.Save(SampleResult(), Path.Combine(_folder.Root, "out"));

        Assert.Equal("MissingFiles_20260927_140312.json", Path.GetFileName(path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ExistingFileIsNeverOverwritten()
    {
        var folder = Path.Combine(_folder.Root, "out");

        var first = ScanResultFile.Save(SampleResult(), folder);
        var second = ScanResultFile.Save(SampleResult(), folder);
        var third = ScanResultFile.Save(SampleResult(), folder);

        Assert.Equal("MissingFiles_20260927_140312_2.json", Path.GetFileName(second));
        Assert.Equal("MissingFiles_20260927_140312_3.json", Path.GetFileName(third));
        Assert.True(File.Exists(first));
    }

    [Fact]
    public void RoundTripKeepsAllValues()
    {
        var original = SampleResult();

        var loaded = ScanResultFile.Load(ScanResultFile.Save(original, _folder.Root));

        Assert.Equal(original.ScanStarted, loaded.ScanStarted);
        Assert.Equal(original.ScanFinished, loaded.ScanFinished);
        Assert.Equal(original.SourceRoot, loaded.SourceRoot);
        Assert.Equal(original.DestinationRoot, loaded.DestinationRoot);
        Assert.Equal(original.FileTypesFilePath, loaded.FileTypesFilePath);
        Assert.Equal(original.Extensions, loaded.Extensions);
        Assert.Equal(original.Summary, loaded.Summary);
        Assert.Equal(original.MissingFiles, loaded.MissingFiles);
        Assert.Equal(original.Errors, loaded.Errors);
        Assert.Equal(DateTimeKind.Utc, loaded.MissingFiles[0].LastWriteTimeUtc.Kind);
        Assert.Equal(TimeSpan.FromSeconds(88), loaded.Duration);
    }

    [Fact]
    public void JsonUsesPropertyNamesFromSpecification() // REQ-19
    {
        var path = ScanResultFile.Save(SampleResult(), _folder.Root);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("NameAndSize", root.GetProperty("matchMode").GetString());
        Assert.Equal(@"C:\Users\x\FileTypes.json", root.GetProperty("fileTypesFile").GetString());
        Assert.Equal("2026-09-27T14:03:12+02:00", root.GetProperty("scanStarted").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("sameNameDifferentSize").GetInt32());
        var missing = root.GetProperty("missingFiles")[0];
        Assert.Equal(@"2021\Bjørn æøå.JPG", missing.GetProperty("relativePath").GetString());
        Assert.Equal(4839211, missing.GetProperty("sizeBytes").GetInt64());
        Assert.Equal("2021-07-14T10:22:05Z", missing.GetProperty("lastWriteTimeUtc").GetString());
        Assert.True(missing.GetProperty("sameNameDifferentSize").GetBoolean());
        Assert.Equal("Access denied", root.GetProperty("errors")[0].GetProperty("message").GetString());
        Assert.False(root.TryGetProperty("duration", out _));
    }

    [Fact]
    public void NonAsciiFileNamesAreWrittenReadably()
    {
        var text = File.ReadAllText(ScanResultFile.Save(SampleResult(), _folder.Root));

        Assert.Contains("Bjørn æøå.JPG", text, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidJsonReportsFileAndLine()
    {
        var path = Path.Combine(_folder.Root, "broken.json");
        File.WriteAllText(path, "{\n  \"schemaVersion\": 1,\n  oops\n}");

        var ex = Assert.Throws<ScanResultFileException>(() => ScanResultFile.Load(path));

        Assert.Contains(path, ex.Message, StringComparison.Ordinal);
        Assert.Contains("line 3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRequiredPropertyIsRejected()
    {
        var path = Path.Combine(_folder.Root, "incomplete.json");
        File.WriteAllText(path, """{ "schemaVersion": 1, "sourceRoot": "C:\\a" }""");

        Assert.Throws<ScanResultFileException>(() => ScanResultFile.Load(path));
    }

    [Fact]
    public void UnsupportedSchemaVersionIsRejected()
    {
        var path = ScanResultFile.Save(SampleResult(), _folder.Root);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 7", StringComparison.Ordinal));

        var ex = Assert.Throws<ScanResultFileException>(() => ScanResultFile.Load(path));

        Assert.Contains("schemaVersion 7 is not supported", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    public void EmptyOrNullFileIsRejected(string content)
    {
        var path = Path.Combine(_folder.Root, "empty.json");
        File.WriteAllText(path, content);

        Assert.Throws<ScanResultFileException>(() => ScanResultFile.Load(path));
    }

    [Fact]
    public void MissingFileIsReported()
    {
        var path = Path.Combine(_folder.Root, "nope.json");

        var ex = Assert.Throws<ScanResultFileException>(() => ScanResultFile.Load(path));

        Assert.Contains("could not be read", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultOutputFolderIsInDocuments()
    {
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MissingFiles"),
            ScanResultFile.DefaultOutputFolder);
    }

    private static ScanResult SampleResult()
    {
        var started = new DateTimeOffset(2026, 9, 27, 14, 3, 12, TimeSpan.FromHours(2));
        return new ScanResult
        {
            ScanStarted = started,
            ScanFinished = started.AddSeconds(88),
            SourceRoot = @"D:\CameraBackup",
            DestinationRoot = @"E:\PhotoArchive",
            FileTypesFilePath = @"C:\Users\x\FileTypes.json",
            Extensions = [".jpg", ".mp4"],
            Summary = new ScanSummary(12450, 12310, 138, 1, 17, 1),
            MissingFiles =
            [
                new MissingFile(@"2021\Bjørn æøå.JPG", 4839211, new DateTime(2021, 7, 14, 10, 22, 5, DateTimeKind.Utc), true),
                new MissingFile(@"2022\VID_01.MP4", 10, new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc), false),
            ],
            Errors = [new ScanError(@"D:\CameraBackup\locked", "Access denied")],
        };
    }
}
