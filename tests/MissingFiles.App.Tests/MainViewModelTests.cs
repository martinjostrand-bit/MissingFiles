using MissingFiles.App.Services;
using MissingFiles.App.ViewModels;
using MissingFiles.Core.Scanning;

namespace MissingFiles.App.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private const string PdfOnly = """
        { "schemaVersion": 1, "groups": [ { "name": "Docs", "extensions": [".pdf"] } ] }
        """;

    private readonly TestFolder _folder = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeShellService _shell = new();
    private readonly InMemorySettingsStore _settings = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void FirstStartCreatesUserFileTypesFileAndLoadsDefaultGroups() // REQ-05b, REQ-06
    {
        var vm = CreateInitialized();

        Assert.True(File.Exists(_folder.UserFileTypes));
        Assert.Equal(_folder.UserFileTypes, vm.FileTypesFilePath);
        Assert.Equal(["Pictures", "Videos", "Documents"], vm.Groups.Select(g => g.Name));
        Assert.Equal([true, true, false], vm.Groups.Select(g => g.IsChecked));
        Assert.StartsWith(".jpg .jpeg", vm.Groups[0].ExtensionsText, StringComparison.Ordinal);
        Assert.False(vm.HasFileTypesError);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public void SettingsAreRestored() // REQ-04, REQ-05c
    {
        var typesFile = _folder.WriteFile("types.json", PdfOnly);
        _settings.Settings = new AppSettings
        {
            SourceFolder = _folder.Source,
            DestinationFolder = _folder.Destination,
            OutputFolder = _folder.Output,
            FileTypesFile = typesFile,
        };

        var vm = CreateInitialized();

        Assert.Equal(_folder.Source, vm.SourceFolder);
        Assert.Equal(_folder.Destination, vm.DestinationFolder);
        Assert.Equal(_folder.Output, vm.OutputFolder);
        Assert.Equal(typesFile, vm.FileTypesFilePath);
        Assert.Equal("Docs", Assert.Single(vm.Groups).Name);
    }

    [Fact]
    public void RemovedFileTypesFileFallsBackToUserFile()
    {
        _settings.Settings = new AppSettings { FileTypesFile = Path.Join(_folder.Root, "gone.json") };

        var vm = CreateInitialized();

        Assert.Equal(_folder.UserFileTypes, vm.FileTypesFilePath);
    }

    [Fact]
    public void InvalidFileTypesFileShowsErrorAndDisablesScan() // REQ-05d, TC-23
    {
        _folder.WriteFile(Path.GetRelativePath(_folder.Root, _folder.UserFileTypes), "{ broken");
        var vm = CreateInitialized();
        vm.SourceFolder = _folder.Source;
        vm.DestinationFolder = _folder.Destination;

        Assert.True(vm.HasFileTypesError);
        Assert.Contains(_folder.UserFileTypes, vm.FileTypesError, StringComparison.Ordinal);
        Assert.Contains("Reload", vm.FileTypesError, StringComparison.Ordinal);
        Assert.Empty(vm.Groups);
        Assert.False(vm.StartScanCommand.CanExecute(null));
    }

    [Fact]
    public void ReloadReadsTheEditedFile() // REQ-06
    {
        var vm = CreateInitialized();
        File.WriteAllText(_folder.UserFileTypes, PdfOnly);

        vm.ReloadFileTypesCommand.Execute(null);

        Assert.Equal("Docs", Assert.Single(vm.Groups).Name);
    }

    [Fact]
    public void ChooseFileTypesFileLoadsAndRemembersIt() // REQ-05c
    {
        var vm = CreateInitialized();
        var typesFile = _folder.WriteFile("types.json", PdfOnly);
        _dialogs.FileAnswers.Enqueue(typesFile);

        vm.ChooseFileTypesFileCommand.Execute(null);

        Assert.Equal(typesFile, vm.FileTypesFilePath);
        Assert.Equal(typesFile, _settings.Settings.FileTypesFile);
    }

    [Fact]
    public void EditOpensFileTypesFileInEditor() // REQ-06
    {
        var vm = CreateInitialized();

        vm.OpenFileTypesFileCommand.Execute(null);

        Assert.Equal(_folder.UserFileTypes, Assert.Single(_shell.OpenedInEditor));
    }

    [Fact]
    public void BrowseSetsFoldersAndCancelKeepsThem() // REQ-01
    {
        var vm = CreateInitialized();
        _dialogs.FolderAnswers.Enqueue(_folder.Source);
        _dialogs.FolderAnswers.Enqueue(null);

        vm.BrowseSourceCommand.Execute(null);
        vm.BrowseSourceCommand.Execute(null);

        Assert.Equal(_folder.Source, vm.SourceFolder);
    }

    [Fact]
    public void StartScanNeedsBothFolders()
    {
        var vm = CreateInitialized();

        Assert.False(vm.StartScanCommand.CanExecute(null));
        vm.SourceFolder = _folder.Source;
        Assert.False(vm.StartScanCommand.CanExecute(null));
        vm.DestinationFolder = _folder.Destination;
        Assert.True(vm.StartScanCommand.CanExecute(null));
    }

    [Fact]
    public async Task ScanShowsProgressSavesAndShowsResult() // REQ-12, REQ-13, REQ-18, REQ-20
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource(@"2021\IMG_0002.JPG");
        _folder.AddDestination(@"x\IMG_0001.JPG");
        var vm = CreateReadyToScan();

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.Errors);
        Assert.IsType<ScanProgressViewModel>(Assert.Single(_dialogs.ProgressDialogs));
        var result = Assert.IsType<ScanResultViewModel>(vm.Result);
        Assert.True(vm.HasResult);
        Assert.Equal(new ScanSummary(2, 1, 1, 0, 0, 0), result.Summary);
        Assert.Equal(Assert.Single(_folder.OutputFiles()), result.ResultFilePath);
        Assert.Equal(@"2021\IMG_0002.JPG", Assert.Single(result.VisibleMissingFiles).RelativePath);
        Assert.Contains("1 missing", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanRemembersFolders() // REQ-04
    {
        var vm = CreateReadyToScan();

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Equal(_folder.Source, _settings.Settings.SourceFolder);
        Assert.Equal(_folder.Destination, _settings.Settings.DestinationFolder);
        Assert.Equal(_folder.Output, _settings.Settings.OutputFolder);
    }

    [Fact]
    public async Task CheckboxesOverrideEnabledGroups() // REQ-06, TC-22
    {
        _folder.AddSource("IMG_0001.JPG");
        _folder.AddSource("report.pdf");
        var vm = CreateReadyToScan();
        vm.Groups.Single(g => g.Name == "Pictures").IsChecked = false;
        vm.Groups.Single(g => g.Name == "Documents").IsChecked = true;

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Equal("report.pdf", Assert.Single(vm.Result!.VisibleMissingFiles).RelativePath);
    }

    [Fact]
    public async Task NoGroupCheckedShowsError()
    {
        var vm = CreateReadyToScan();
        foreach (var group in vm.Groups)
        {
            group.IsChecked = false;
        }

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Contains("No file types are selected", Assert.Single(_dialogs.Errors).Message, StringComparison.Ordinal);
        Assert.Empty(_dialogs.ProgressDialogs);
    }

    [Theory]
    [InlineData("missing source")]
    [InlineData("nested")]
    public async Task InvalidFoldersShowErrorWithoutProgressDialog(string scenario) // REQ-02
    {
        var vm = CreateReadyToScan();
        if (scenario == "missing source")
        {
            vm.SourceFolder = Path.Join(_folder.Root, "nope");
        }
        else
        {
            vm.DestinationFolder = Directory.CreateDirectory(Path.Join(_folder.Source, "inner")).FullName;
        }

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Equal("Cannot start the scan", Assert.Single(_dialogs.Errors).Title);
        Assert.Empty(_dialogs.ProgressDialogs);
        Assert.Null(vm.Result);
    }

    [Fact]
    public async Task CancelledScanWritesNoResultFile() // REQ-15
    {
        _folder.AddSource("IMG_0001.JPG");
        var vm = CreateReadyToScan();
        _dialogs.OnProgressShown = progress => progress.CancelCommand.Execute(null);

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Empty(_folder.OutputFiles());
        Assert.Null(vm.Result);
        Assert.Contains("cancelled", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task UnwritableOutputFolderShowsError()
    {
        var vm = CreateReadyToScan();
        vm.OutputFolder = _folder.WriteFile("a-file-not-a-folder", "x");

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Equal("Cannot save the result file", Assert.Single(_dialogs.Errors).Title);
        Assert.Null(vm.Result);
    }

    [Fact]
    public async Task OpenResultFileShowsEarlierResult() // REQ-21a
    {
        _folder.AddSource("IMG_0001.JPG");
        var vm = CreateReadyToScan();
        await vm.StartScanCommand.ExecuteAsync(null);
        var resultFile = vm.Result!.ResultFilePath;

        var other = CreateInitialized();
        _dialogs.FileAnswers.Enqueue(resultFile);
        other.OpenResultFileCommand.Execute(null);

        Assert.Equal(resultFile, other.Result!.ResultFilePath);
        Assert.Equal(1, other.Result.Summary.Missing);
    }

    [Fact]
    public void OpenInvalidResultFileShowsError()
    {
        var vm = CreateInitialized();
        _dialogs.FileAnswers.Enqueue(_folder.WriteFile("broken.json", "{"));

        vm.OpenResultFileCommand.Execute(null);

        Assert.Equal("Cannot open the scan result file", Assert.Single(_dialogs.Errors).Title);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void JsonSettingsStoreRoundTripsAndToleratesBrokenFile()
    {
        var path = Path.Join(_folder.Root, "settings", "settings.json");
        var store = new JsonSettingsStore(path);
        var settings = new AppSettings { SourceFolder = @"D:\a", OutputFolder = @"E:\b" };

        Assert.Equal(new AppSettings(), store.Load());
        store.Save(settings);
        Assert.Equal(settings, store.Load());

        File.WriteAllText(path, "{ broken");
        Assert.Equal(new AppSettings(), store.Load());
    }

    private MainViewModel CreateInitialized()
    {
        var vm = new MainViewModel(_dialogs, _settings, _shell, userFileTypesPath: _folder.UserFileTypes);
        vm.Initialize();
        return vm;
    }

    private MainViewModel CreateReadyToScan()
    {
        var vm = CreateInitialized();
        vm.SourceFolder = _folder.Source;
        vm.DestinationFolder = _folder.Destination;
        vm.OutputFolder = _folder.Output;
        return vm;
    }
}
