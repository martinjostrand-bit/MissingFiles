using System.Security.Cryptography;

using MissingFiles.App.Services;
using MissingFiles.Core.FileTypes;
using MissingFiles.Core.Scanning;

namespace MissingFiles.UiTests;

/// <summary>The data folder of the desktop app (spec FT-13, REQ-04a).</summary>
public sealed class DataFolderTests : IDisposable
{
    private static readonly string RealDataFolder =
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MissingFiles");

    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void AppKeepsItsFilesInTheDataFolder() // FT-13, REQ-04, REQ-04a, REQ-05b
    {
        Directory.CreateDirectory(_folder.DataFolder);
        var realFiles = ReadRealFiles();

        using (var app = new AppSession(_folder))
        {
            app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);
            app.Close();
        }

        Assert.True(File.Exists(Path.Join(_folder.DataFolder, "settings.json")));
        Assert.True(File.Exists(Path.Join(_folder.DataFolder, "FileTypes.json")));
        Assert.Equal(realFiles, ReadRealFiles());
    }

    [Fact]
    public void AppCreatesADataFolderThatDoesNotExist() // REQ-04a
    {
        Assert.False(Directory.Exists(_folder.DataFolder));

        using var app = new AppSession(_folder);

        Assert.True(File.Exists(_folder.UserFileTypes));
    }

    [Fact]
    public void EmptyValueCountsAsNotSet() // FT-13, REQ-04a
    {
        // Only read here. The app shows what is in the default location (REQ-04, REQ-05b).
        var settings = new JsonSettingsStore(JsonSettingsStore.DefaultPath).Load();
        var fileTypes = settings.FileTypesFile is { } saved && File.Exists(saved) ? saved : FileTypesFile.UserFilePath;

        // This session sees the real settings, so it ends without Close, which would save over them.
        using var app = new AppSession(_folder, dataFolderValue: string.Empty);

        Assert.Equal(settings.SourceFolder ?? string.Empty, app.TextBoxValue("SourceFolder"));
        Assert.Equal(fileTypes, app.TextBoxValue("FileTypesFile"));
        Assert.Empty(Directory.GetFiles(_folder.Root));
    }

    [Fact]
    public void DataFolderDoesNotChangeTheOutputFolder() // REQ-04a, REQ-18
    {
        using var app = new AppSession(_folder);

        Assert.Equal(ScanResultFile.DefaultOutputFolder, app.TextBoxValue("OutputFolder"));
    }

    [Theory]
    [InlineData(@"relative\folder")]
    [InlineData("C:drive-relative")]
    [InlineData(@"\\server")]
    [InlineData(@"\\")]
    public void ValueThatIsNotAnAbsolutePathStopsTheStart(string value) // FT-13, REQ-04a
    {
        var realFiles = ReadRealFiles();
        using var app = new AppSession(_folder, value);

        Assert.False(app.Exists("SourceFolder"));
        var message = app.DismissStartError();

        Assert.Contains(value, message, StringComparison.Ordinal);
        Assert.DoesNotContain(AppSession.DataFolderVariable, message, StringComparison.Ordinal);
        Assert.NotEqual(0, app.ExitCode);
        Assert.Empty(Directory.GetFiles(_folder.Root));
        Assert.False(Directory.Exists(Path.Join(_folder.Root, "relative")));
        Assert.Equal(realFiles, ReadRealFiles());
    }

    [Fact]
    public void FolderThatCannotBeCreatedIsReportedInTheFileTypesArea() // REQ-04a, REQ-05d
    {
        // This session has no usable data folder, so it ends without Close, like the other sessions with a value.
        using var app = new AppSession(_folder, Path.Join(_folder.Root, "not|valid"));

        Assert.False(string.IsNullOrEmpty(app.Text("FileTypesError")));
        Assert.False(app.IsEnabled("StartScan"));
    }

    /// <summary>The name, write time and content of each file in <c>%APPDATA%\MissingFiles</c>.</summary>
    private static Dictionary<string, string> ReadRealFiles() =>
        !Directory.Exists(RealDataFolder)
            ? []
            : Directory.GetFiles(RealDataFolder).ToDictionary(
                file => file,
                file => $"{File.GetLastWriteTimeUtc(file):O} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))}");
}
