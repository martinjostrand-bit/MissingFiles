namespace MissingFiles.UiTests;

/// <summary>Smoke tests of the main window before a scan (spec §3.4).</summary>
public sealed class StartupTests : IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void FirstStartShowsTheDefaultFileTypes() // REQ-01, REQ-05b, REQ-06
    {
        using var app = new AppSession(_folder);

        Assert.Equal("MissingFiles", app.MainWindow.Title);
        Assert.Equal(_folder.UserFileTypes, app.TextBoxValue("FileTypesFile"));
        Assert.True(File.Exists(_folder.UserFileTypes));
        Assert.True(app.GroupCheckBox("Pictures").IsChecked);
        Assert.True(app.GroupCheckBox("Videos").IsChecked);
        Assert.False(app.Exists("FilesScanned"));
    }

    [Fact]
    public void PageThatScrollsHasAName() // NFR-08
    {
        using var app = new AppSession(_folder);

        Assert.Equal("Main window content", app.Text("Page"));
    }

    [Fact]
    public void ScanNeedsBothFolders() // REQ-01
    {
        using var app = new AppSession(_folder);
        Assert.False(app.IsEnabled("StartScan"));

        app.SetText("SourceFolder", _folder.Source);
        Assert.False(app.IsEnabled("StartScan"));

        app.SetText("DestinationFolder", _folder.Destination);
        app.WaitUntil("Start scan is enabled", () => app.IsEnabled("StartScan"));
    }

    [Fact]
    public void FoldersAreRememberedForTheNextStart() // REQ-04, REQ-05c
    {
        using (var first = new AppSession(_folder))
        {
            first.SetFolders(_folder.Source, _folder.Destination, _folder.Output);
            first.Close();
        }

        using var second = new AppSession(_folder);

        Assert.Equal(_folder.Source, second.TextBoxValue("SourceFolder"));
        Assert.Equal(_folder.Destination, second.TextBoxValue("DestinationFolder"));
        Assert.Equal(_folder.Output, second.TextBoxValue("OutputFolder"));
        Assert.Equal(_folder.UserFileTypes, second.TextBoxValue("FileTypesFile"));
    }

    [Fact]
    public void InvalidFileTypesFileBlocksTheScanUntilItIsFixed() // REQ-05d, REQ-06
    {
        _folder.WriteUserFileTypes("{ not json");
        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);

        Assert.Contains("FileTypes.json", app.Text("FileTypesError"), StringComparison.Ordinal);
        Assert.False(app.IsEnabled("StartScan"));

        _folder.WriteUserFileTypes("""{ "schemaVersion": 1, "groups": [ { "name": "Pdf", "extensions": [ ".pdf" ] } ] }""");
        app.Press("ReloadFileTypes");

        app.WaitUntil("the error is gone", () => !app.Exists("FileTypesError"));
        Assert.True(app.GroupCheckBox("Pdf").IsChecked);
        app.WaitUntil("Start scan is enabled", () => app.IsEnabled("StartScan"));
    }
}
