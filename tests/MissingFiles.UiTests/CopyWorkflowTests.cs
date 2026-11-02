using System.Globalization;
using System.Text.RegularExpressions;

using FlaUI.Core.AutomationElements;

using MissingFiles.Core.Copying;

namespace MissingFiles.UiTests;

/// <summary>Smoke tests of the copy workflow in the real app (spec §3.4, §4.7, §4.8).</summary>
public sealed class CopyWorkflowTests : IDisposable
{
    /// <summary>Enough files for the progress dialog to stay open long enough to press Cancel.</summary>
    private const int FilesForCancel = 5000;

    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void CopyAsksCopiesAndShowsTheResult() // REQ-23, REQ-24, REQ-25, REQ-26, REQ-31, REQ-32
    {
        _folder.AddSource(@"2021\Summer\IMG_0001.JPG", size: 100);
        _folder.AddSource("IMG_0002.JPG", size: 20);
        var existing = _folder.AddDestination("IMG_0002.JPG", size: 30);
        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);
        app.Scan();

        app.Press("StartCopy");
        var question = app.AnswerConfirmation("Copy missing files", ok: true);

        Assert.Contains("Copy 2 files", question, StringComparison.Ordinal);
        Assert.Contains(_folder.Source, question, StringComparison.Ordinal);
        Assert.Contains(_folder.Destination, question, StringComparison.Ordinal);

        var result = app.Dialog("Copy result");
        Assert.Equal("Copied: 1", app.Find(result, "CopyCopied").Name);
        Assert.Equal("Skipped, exists: 1", app.Find(result, "CopySkippedExists").Name);
        Assert.Equal("Skipped, source changed: 0", app.Find(result, "CopySkippedChanged").Name);
        Assert.Equal("Failed: 0", app.Find(result, "CopyFailed").Name);
        Assert.Equal("Data copied: 100 bytes", app.Find(result, "CopyBytes").Name);

        var log = Assert.Single(_folder.OutputFiles($"{CopyLogFile.FileNamePrefix}*.log"));
        Assert.Equal(log, app.Find(result, "CopyLogPath").AsTextBox().Text);
        Assert.Equal(100, new FileInfo(Path.Join(_folder.Destination, "2021", "Summer", "IMG_0001.JPG")).Length);
        Assert.Equal(30, new FileInfo(existing).Length);

        app.Press(result, "CloseButton");
        app.WaitUntil("the copy result is closed", () => !app.DialogIsOpen("Copy result"));
        Assert.StartsWith("Copy finished: 1 copied, 1 skipped, 0 failed.", app.Status, StringComparison.Ordinal);

        app.Press("ShowLastCopy");
        app.Press(app.Dialog("Copy result"), "CloseButton");
    }

    [Fact]
    public void CopyThatIsNotConfirmedCopiesNothing() // REQ-24
    {
        _folder.AddSource("IMG_0001.JPG");
        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);
        app.Scan();

        app.Press("StartCopy");
        app.AnswerConfirmation("Copy missing files", ok: false);

        Assert.False(File.Exists(Path.Join(_folder.Destination, "IMG_0001.JPG")));
        Assert.Empty(_folder.OutputFiles($"{CopyLogFile.FileNamePrefix}*"));
    }

    [Fact]
    public void CopyIsNotOfferedWhenNothingIsMissing() // REQ-22
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddDestination("IMG_0001.JPG");
        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);

        app.Scan();

        Assert.Equal("Missing: 0", app.Text("Missing"));
        Assert.False(app.IsEnabled("StartCopy"));
    }

    [Fact]
    public void CancelStopsTheCopyAndLeavesNoPartialFiles() // REQ-28, REQ-30, REQ-31
    {
        for (var i = 0; i < FilesForCancel; i++)
        {
            _folder.AddSource($"IMG_{i:D4}.JPG");
        }

        using var app = new AppSession(_folder);
        app.SetFolders(_folder.Source, _folder.Destination, _folder.Output);
        app.Scan();

        app.Press("StartCopy");
        app.AnswerConfirmation("Copy missing files", ok: true);

        var progress = app.Dialog("Copying");
        var labels = AppSession.DialogTexts(progress);
        foreach (var label in new[] { "Files: ", "Data: ", "Skipped: ", "Failed: ", "Time remaining: ", "Elapsed time: " })
        {
            Assert.Contains(labels, text => text.StartsWith(label, StringComparison.Ordinal));
        }

        app.Press(progress, "CancelButton");

        var result = app.Dialog("Copy result");
        Assert.StartsWith("Copy cancelled after", app.Find(result, "CopyHeadline").Name, StringComparison.Ordinal);
        var copied = Number(app.Find(result, "CopyCopied").Name);
        var notProcessed = Number(app.Find(result, "CopyNotProcessed").Name);
        Assert.True(notProcessed > 0, $"{notProcessed} files not processed.");
        Assert.Equal(FilesForCancel, copied + notProcessed);

        var written = Directory.GetFiles(_folder.Destination);
        Assert.Equal(copied, written.Length);
        Assert.All(written, file => Assert.EndsWith(".JPG", file, StringComparison.Ordinal));
        Assert.Single(_folder.OutputFiles($"{CopyLogFile.FileNamePrefix}*.log"));
    }

    /// <summary>The number in "Label: 1,234", whatever the digit grouping.</summary>
    private static int Number(string text) => int.Parse(Regex.Replace(text, @"\D", string.Empty), CultureInfo.InvariantCulture);
}
