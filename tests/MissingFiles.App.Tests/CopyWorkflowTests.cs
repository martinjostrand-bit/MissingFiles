using MissingFiles.App.ViewModels;
using MissingFiles.Core.Copying;

namespace MissingFiles.App.Tests;

/// <summary>The copy workflow of the main window (spec §4.7, §4.8).</summary>
public sealed class CopyWorkflowTests : IDisposable
{
    private readonly TestFolder _folder = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeShellService _shell = new();
    private readonly InMemorySettingsStore _settings = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task CopyIsOnlyPossibleWithMissingFiles()
    {
        var vm = CreateReadyToScan();
        Assert.False(vm.StartCopyCommand.CanExecute(null));

        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Equal(0, vm.Result!.Summary.Missing);
        Assert.False(vm.StartCopyCommand.CanExecute(null));

        _folder.AddSource("IMG_0001.JPG");
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.True(vm.StartCopyCommand.CanExecute(null));
    }

    [Fact]
    public async Task ResultShowsWhatACopyWouldDo() // REQ-22, REQ-23
    {
        _folder.AddSource("IMG_0001.JPG", size: 100);
        _folder.AddSource(@"2021\IMG_0002.JPG", size: 50);
        var vm = await ScanAsync();

        var preview = vm.Result!.CopyPreview!;
        Assert.Equal(_folder.Source, preview.SourceRoot);
        Assert.Equal(_folder.Destination, preview.DestinationRoot);
        Assert.Equal(2, preview.FileCount);
        Assert.Equal(150, preview.TotalBytes);
        Assert.NotNull(preview.FreeBytes);
        Assert.Equal("150 bytes", vm.Result.TotalSizeText);
        Assert.Null(vm.Result.CopyProblem);
    }

    [Fact]
    public async Task CopyAsksCopiesWritesLogAndShowsResult() // REQ-24, REQ-25, REQ-30, REQ-31, REQ-32
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource(@"2021\Summer\IMG_0002.JPG", size: 20);
        _folder.AddDestination(@"x\IMG_0001.JPG");
        var vm = await ScanAsync();

        await vm.StartCopyCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.Errors);
        var (title, message) = Assert.Single(_dialogs.Confirmations);
        Assert.Equal("Copy missing files", title);
        Assert.Contains("Copy 1 file (20 bytes)?", message, StringComparison.Ordinal);
        Assert.Contains(_folder.Source, message, StringComparison.Ordinal);
        Assert.Contains(_folder.Destination, message, StringComparison.Ordinal);
        Assert.IsType<CopyProgressViewModel>(_dialogs.ProgressDialogs[^1]);

        var copied = Path.Join(_folder.Destination, @"2021\Summer\IMG_0002.JPG");
        Assert.Equal(20, new FileInfo(copied).Length);
        Assert.Equal(
            File.GetLastWriteTimeUtc(Path.Join(_folder.Source, @"2021\Summer\IMG_0002.JPG")),
            File.GetLastWriteTimeUtc(copied));

        var copy = Assert.IsType<CopyResultViewModel>(vm.Result!.LastCopy);
        Assert.True(vm.Result.HasLastCopy);
        Assert.Same(copy, Assert.Single(_dialogs.CopyResultDialogs));
        Assert.Equal(new CopySummary(1, 1, 0, 0, 0, 0, 20), copy.Summary);
        Assert.False(copy.Result.Cancelled);
        Assert.False(copy.HasProblems);
        Assert.StartsWith("Copy finished", copy.HeadlineText, StringComparison.Ordinal);

        var log = Assert.Single(_folder.OutputFiles(), f => f.EndsWith(".log", StringComparison.Ordinal));
        Assert.Equal(log, copy.LogFilePath);
        Assert.StartsWith(CopyLogFile.FileNamePrefix, Path.GetFileName(log), StringComparison.Ordinal);
        Assert.Contains(@"Copied                   | 2021\Summer\IMG_0002.JPG", File.ReadAllText(log), StringComparison.Ordinal);
        Assert.Equal("Copy finished: 1 copied, 0 skipped, 0 failed.", vm.StatusMessage);
    }

    [Fact]
    public async Task DeclinedConfirmationCopiesNothing()
    {
        _folder.AddSource("IMG_0001.JPG");
        var vm = await ScanAsync();
        _dialogs.ConfirmAnswers.Enqueue(false);

        await vm.StartCopyCommand.ExecuteAsync(null);

        Assert.False(File.Exists(Path.Join(_folder.Destination, "IMG_0001.JPG")));
        Assert.Null(vm.Result!.LastCopy);
        Assert.Single(_dialogs.ProgressDialogs); // only the scan
        Assert.DoesNotContain(_folder.OutputFiles(), f => f.EndsWith(".log", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExistingFileIsNeverOverwritten() // REQ-26, TC-15
    {
        _folder.AddSource(@"2021\IMG_0001.JPG", size: 10);
        _folder.AddDestination(@"2021\IMG_0001.JPG", size: 20);
        var vm = await ScanAsync();
        Assert.True(Assert.Single(vm.Result!.VisibleMissingFiles).SameNameDifferentSize);

        await vm.StartCopyCommand.ExecuteAsync(null);

        Assert.Equal(20, new FileInfo(Path.Join(_folder.Destination, @"2021\IMG_0001.JPG")).Length);
        var copy = vm.Result.LastCopy!;
        Assert.Equal(1, copy.Summary.SkippedExists);
        var problem = Assert.Single(copy.Problems);
        Assert.Equal("Skipped - exists", problem.Status);
        Assert.Equal(@"2021\IMG_0001.JPG", problem.RelativePath);
        Assert.Equal("Copy finished: 0 copied, 1 skipped, 0 failed.", vm.StatusMessage);
    }

    [Fact]
    public async Task ChangedSourceFileIsSkipped() // REQ-27
    {
        var source = _folder.AddSource("IMG_0001.JPG", size: 10);
        var vm = await ScanAsync();
        File.WriteAllBytes(source, new byte[11]);

        await vm.StartCopyCommand.ExecuteAsync(null);

        Assert.False(File.Exists(Path.Join(_folder.Destination, "IMG_0001.JPG")));
        Assert.Equal("Skipped - source changed", Assert.Single(vm.Result!.LastCopy!.Problems).Status);
    }

    [Fact]
    public async Task CancelStopsCopyAndStillWritesLog() // REQ-30, REQ-32
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("IMG_0002.JPG");
        var vm = await ScanAsync();
        _dialogs.OnProgressShown = progress => progress.CancelCommand.Execute(null);

        await vm.StartCopyCommand.ExecuteAsync(null);

        Assert.Empty(Directory.GetFiles(_folder.Destination, "*", SearchOption.AllDirectories));
        var copy = vm.Result!.LastCopy!;
        Assert.True(copy.Result.Cancelled);
        Assert.True(copy.HasNotProcessed);
        Assert.Equal(2, copy.Summary.NotProcessed);
        Assert.StartsWith("Copy cancelled", copy.HeadlineText, StringComparison.Ordinal);
        Assert.NotNull(copy.LogFilePath);
        Assert.Contains("Cancelled:                 Yes", File.ReadAllText(copy.LogFilePath), StringComparison.Ordinal);
        Assert.StartsWith("Copy cancelled", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovedSourceFolderShowsErrorWithoutCopying() // REQ-02 for copy
    {
        _folder.AddSource("IMG_0001.JPG");
        var vm = await ScanAsync();
        Directory.Delete(_folder.Source, recursive: true);

        await vm.StartCopyCommand.ExecuteAsync(null);

        var (title, message) = Assert.Single(_dialogs.Errors);
        Assert.Equal("Cannot start the copy", title);
        Assert.Equal(message, vm.Result!.CopyProblem);
        Assert.Empty(_dialogs.Confirmations);
        Assert.Single(_dialogs.ProgressDialogs);
        Assert.Null(vm.Result.LastCopy);
    }

    [Fact]
    public async Task UnwritableLogFolderShowsErrorButKeepsResult()
    {
        _folder.AddSource("IMG_0001.JPG");
        var vm = await ScanAsync();
        vm.OutputFolder = _folder.WriteFile("a-file-not-a-folder", "x");

        await vm.StartCopyCommand.ExecuteAsync(null);

        Assert.Equal("Cannot save the copy log", Assert.Single(_dialogs.Errors).Title);
        Assert.True(File.Exists(Path.Join(_folder.Destination, "IMG_0001.JPG")));
        var copy = vm.Result!.LastCopy!;
        Assert.Null(copy.LogFilePath);
        Assert.False(copy.OpenLogFolderCommand.CanExecute(null));
    }

    [Fact]
    public async Task CopyFromOpenedResultFile() // REQ-21a, REQ-22
    {
        _folder.AddSource("IMG_0001.JPG");
        var resultFile = (await ScanAsync()).Result!.ResultFilePath;

        var vm = CreateReadyToScan();
        _dialogs.FileAnswers.Enqueue(resultFile);
        vm.OpenResultFileCommand.Execute(null);
        await vm.StartCopyCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Join(_folder.Destination, "IMG_0001.JPG")));
        Assert.Equal(resultFile, vm.Result!.LastCopy!.Result.ScanResultFilePath);
    }

    [Fact]
    public async Task LastCopyResultCanBeShownAgain() // REQ-31
    {
        _folder.AddSource("IMG_0001.JPG");
        var vm = await ScanAsync();
        Assert.False(vm.ShowLastCopyCommand.CanExecute(null));

        await vm.StartCopyCommand.ExecuteAsync(null);
        Assert.True(vm.ShowLastCopyCommand.CanExecute(null));
        vm.ShowLastCopyCommand.Execute(null);

        Assert.Equal(2, _dialogs.CopyResultDialogs.Count);
        Assert.All(_dialogs.CopyResultDialogs, d => Assert.Same(vm.Result!.LastCopy, d));

        // A new scan has not been copied yet.
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.False(vm.ShowLastCopyCommand.CanExecute(null));
    }

    [Fact]
    public async Task ShowLogInFolderUsesShell()
    {
        _folder.AddSource("IMG_0001.JPG");
        var vm = await ScanAsync();
        await vm.StartCopyCommand.ExecuteAsync(null);

        vm.Result!.LastCopy!.OpenLogFolderCommand.Execute(null);

        Assert.Equal(vm.Result.LastCopy.LogFilePath, Assert.Single(_shell.ShownInExplorer));
    }

    private async Task<MainViewModel> ScanAsync()
    {
        var vm = CreateReadyToScan();
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.NotNull(vm.Result);
        return vm;
    }

    private MainViewModel CreateReadyToScan()
    {
        var vm = new MainViewModel(_dialogs, _settings, _shell, userFileTypesPath: _folder.UserFileTypes);
        vm.Initialize();
        vm.SourceFolder = _folder.Source;
        vm.DestinationFolder = _folder.Destination;
        vm.OutputFolder = _folder.Output;
        return vm;
    }
}
