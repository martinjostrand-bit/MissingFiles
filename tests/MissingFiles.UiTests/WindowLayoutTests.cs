namespace MissingFiles.UiTests;

/// <summary>The main window in small sizes (spec §6.5, REQ-21).</summary>
public sealed class WindowLayoutTests : IDisposable
{
    private const int FileCount = 40;
    private const int MinimumListHeight = 150;

    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Theory]
    [InlineData(1024, 688)] // On a CI runner. A 1920 x 1080 display at 150 % scaling gives a little less.
    [InlineData(720, 560)] // The smallest size of the window.
    public void ListIsUsableAndNothingIsOutOfReachInASmallWindow(int width, int height)
    {
        for (var i = 0; i < FileCount; i++)
        {
            _folder.AddSource($"IMG_{i:D4}.JPG");
        }

        using var app = new AppSession(_folder);
        app.MainWindow.Patterns.Transform.Pattern.Resize(width, height);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);
        app.Scan();

        app.ScrollPage(100);

        var list = app.Find("MissingFiles");
        app.WaitUntil("the list has its height", () => list.BoundingRectangle.Height >= MinimumListHeight);
        Assert.InRange(app.MissingFileRows().Length, 4, FileCount - 1); // Shows several rows, and creates only those (NFR-03a).
        Assert.False(list.IsOffscreen);

        app.ScrollPage(0);
        Assert.False(app.Find("SourceFolder").IsOffscreen);
        Assert.False(app.Find("StartScan").IsOffscreen);

        AssertCanBeScrolledIntoView(app, "FilesScanned");
        AssertCanBeScrolledIntoView(app, "StartCopy");
    }

    private static void AssertCanBeScrolledIntoView(AppSession app, string id)
    {
        var element = app.Find(id);
        foreach (var percent in new[] { 0, 25, 50, 75, 100 })
        {
            app.ScrollPage(percent);
            if (!element.IsOffscreen)
            {
                return;
            }
        }

        Assert.Fail($"'{id}' cannot be scrolled into view.");
    }
}
