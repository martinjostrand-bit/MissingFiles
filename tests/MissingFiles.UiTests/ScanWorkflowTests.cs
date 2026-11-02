using MissingFiles.Core.Scanning;

namespace MissingFiles.UiTests;

/// <summary>Smoke tests of the scan workflow in the real app (spec §3.4, §4.4–4.6).</summary>
public sealed class ScanWorkflowTests : IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void ScanShowsTheResultAndWritesTheResultFile() // REQ-12, REQ-18, REQ-20, REQ-21
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddDestination(@"Archive\IMG_0001.JPG");
        _folder.AddSource(@"2021\IMG_0002.JPG");
        _folder.AddSource("IMG_0003.JPG", size: 20);
        _folder.AddDestination("IMG_0003.JPG", size: 30);
        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);

        app.Scan();

        Assert.Equal("Files scanned: 3", app.Text("FilesScanned"));
        Assert.Equal("Matched: 1", app.Text("Matched"));
        Assert.Equal("Missing: 2", app.Text("Missing"));
        Assert.Equal("Same name, other size: 1", app.Text("SameNameDifferentSize"));
        Assert.Equal("Folders not readable: 0", app.Text("Errors"));

        var resultFile = Assert.Single(_folder.OutputFiles("MissingFiles_*.json"));
        Assert.Equal(resultFile, app.TextBoxValue("ResultFilePath"));
        Assert.Equal(2, ScanResultFile.Load(resultFile).Summary.Missing);

        var rows = app.MissingFileRows();
        Assert.Equal(2, rows.Length);
        Assert.Equal(@"2021\IMG_0002.JPG", rows[0][0]);
        Assert.Equal("10 bytes", rows[0][1]);
        Assert.NotEqual("Yes", rows[0][3]);
        Assert.Equal("IMG_0003.JPG", rows[1][0]);
        Assert.Equal("20 bytes", rows[1][1]);
        Assert.Equal("Yes", rows[1][3]);
    }

    [Fact]
    public void FilterNarrowsTheListOfMissingFiles() // REQ-21
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("VID_0002.MP4");
        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);
        app.Scan();
        Assert.Equal("2 files", app.Text("VisibleCount"));

        app.SetText("Filter", "VID");

        app.WaitUntil("the list is filtered", () => app.Text("VisibleCount") == "Showing 1 of 2 files");
        Assert.Equal("VID_0002.MP4", Assert.Single(app.MissingFileRows())[0]);
    }

    [Fact]
    public void GroupThatIsNotTickedIsNotCompared() // REQ-06
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("VID_0002.MP4");
        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);

        app.GroupCheckBox("Pictures").Toggle();
        app.Scan();

        Assert.Equal("Files scanned: 1", app.Text("FilesScanned"));
        Assert.Equal("VID_0002.MP4", Assert.Single(app.MissingFileRows())[0]);
    }

    [Fact]
    public void ScanOfAFolderThatDoesNotExistShowsAnError() // REQ-02
    {
        var missing = Path.Join(_folder.Root, "no such folder");
        using var app = new AppSession(_folder);
        app.SetFolders(missing, _folder.Destination, _folder.Output);

        app.Press("StartScan");

        Assert.Contains(missing, app.DismissMessage("Cannot start the scan"), StringComparison.Ordinal);
        Assert.False(app.Exists("FilesScanned"));
        Assert.Empty(_folder.OutputFiles("*"));
    }

    [Fact]
    public void ScanWithNoGroupTickedShowsAnError() // REQ-05d
    {
        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);
        app.GroupCheckBox("Pictures").Toggle();
        app.GroupCheckBox("Videos").Toggle();

        app.Press("StartScan");

        Assert.Contains("No file types are selected", app.DismissMessage("Cannot start the scan"), StringComparison.Ordinal);
        Assert.Empty(_folder.OutputFiles("*"));
    }

    [Fact]
    public void EarlierResultFileCanBeOpened() // REQ-21a, REQ-22
    {
        _folder.AddSource("IMG_0001.JPG");
        string resultFile;
        using (var first = new AppSession(_folder))
        {
            first.SetFolders(_folder.Source, _folder.Destination, _folder.Output);
            first.Scan();
            resultFile = Assert.Single(_folder.OutputFiles("MissingFiles_*.json"));
            first.Close();
        }

        using var second = new AppSession(_folder);
        Assert.False(second.Exists("FilesScanned"));

        second.Press("OpenResultFile");
        second.ChooseFile("Open a scan result file", resultFile);

        Assert.Equal("Missing: 1", second.Text("Missing"));
        Assert.Equal(resultFile, second.TextBoxValue("ResultFilePath"));
        Assert.Equal("IMG_0001.JPG", Assert.Single(second.MissingFileRows())[0]);
    }

    [Fact]
    public void OpeningAnInvalidResultFileShowsAnError() // REQ-22
    {
        var invalid = Path.Join(_folder.Root, "invalid.json");
        File.WriteAllText(invalid, "{ not json");
        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);

        app.Press("OpenResultFile");
        app.ChooseFile("Open a scan result file", invalid);

        Assert.Contains("invalid.json", app.DismissMessage("Cannot open the scan result file"), StringComparison.Ordinal);
        Assert.False(app.Exists("FilesScanned"));
    }
}
