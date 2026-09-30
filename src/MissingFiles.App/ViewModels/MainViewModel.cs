using System.Collections.ObjectModel;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MissingFiles.App.Services;
using MissingFiles.Core.FileTypes;
using MissingFiles.Core.Scanning;

namespace MissingFiles.App.ViewModels;

/// <summary>
/// The main window: folders, file types, starting a scan and showing its result (spec §4.1–4.6).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const string JsonFilter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

    private readonly IDialogService _dialogs;
    private readonly ISettingsStore _settings;
    private readonly IShellService _shell;
    private readonly TimeProvider _time;
    private readonly string _userFileTypesPath;
    private FileTypesDefinition? _fileTypes;

    public MainViewModel(
        IDialogService dialogs,
        ISettingsStore settings,
        IShellService shell,
        TimeProvider? time = null,
        string? userFileTypesPath = null)
    {
        _dialogs = dialogs;
        _settings = settings;
        _shell = shell;
        _time = time ?? TimeProvider.System;
        _userFileTypesPath = userFileTypesPath ?? FileTypesFile.UserFilePath;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial string SourceFolder { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial string DestinationFolder { get; set; } = string.Empty;

    /// <summary>Where result files are written (REQ-18).</summary>
    [ObservableProperty]
    public partial string OutputFolder { get; set; } = ScanResultFile.DefaultOutputFolder;

    [ObservableProperty]
    public partial string FileTypesFilePath { get; set; } = string.Empty;

    /// <summary>Why the file types file cannot be used, or null if it is valid (REQ-05d).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFileTypesError))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial string? FileTypesError { get; set; }

    public bool HasFileTypesError => FileTypesError is not null;

    public ObservableCollection<FileTypeGroupViewModel> Groups { get; } = [];

    /// <summary>The scan result shown in the result view, or null before the first scan.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    public partial ScanResultViewModel? Result { get; private set; }

    public bool HasResult => Result is not null;

    /// <summary>A short message below the buttons, e.g. that a scan was cancelled.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    /// <summary>
    /// Restores the settings and loads the file types file. On first start the user's
    /// file types file is created from the built-in default (REQ-05b).
    /// </summary>
    public void Initialize()
    {
        var settings = _settings.Load();
        SourceFolder = settings.SourceFolder ?? string.Empty;
        DestinationFolder = settings.DestinationFolder ?? string.Empty;
        OutputFolder = settings.OutputFolder ?? ScanResultFile.DefaultOutputFolder;

        var fileTypesPath = settings.FileTypesFile is { } saved && File.Exists(saved) ? saved : EnsureUserFileTypes();
        LoadFileTypes(fileTypesPath);
    }

    public void SaveSettings() => _settings.Save(new AppSettings
    {
        SourceFolder = NullIfEmpty(SourceFolder),
        DestinationFolder = NullIfEmpty(DestinationFolder),
        OutputFolder = NullIfEmpty(OutputFolder),
        FileTypesFile = NullIfEmpty(FileTypesFilePath),
    });

    [RelayCommand]
    private void BrowseSource() =>
        SourceFolder = _dialogs.PickFolder("Choose the source folder", SourceFolder) ?? SourceFolder;

    [RelayCommand]
    private void BrowseDestination() =>
        DestinationFolder = _dialogs.PickFolder("Choose the destination folder", DestinationFolder) ?? DestinationFolder;

    [RelayCommand]
    private void BrowseOutput() =>
        OutputFolder = _dialogs.PickFolder("Choose where result files are saved", OutputFolder) ?? OutputFolder;

    /// <summary>Chooses another file types file (REQ-05c).</summary>
    [RelayCommand]
    private void ChooseFileTypesFile()
    {
        var path = _dialogs.PickFile("Choose a file types file", JsonFilter, FileTypesFilePath);
        if (path is not null)
        {
            LoadFileTypes(path);
            SaveSettings();
        }
    }

    /// <summary>Opens the file types file in a text editor (REQ-06).</summary>
    [RelayCommand]
    private void OpenFileTypesFile()
    {
        if (!File.Exists(FileTypesFilePath))
        {
            _dialogs.ShowError("File types file", $"The file does not exist: {FileTypesFilePath}");
            return;
        }

        _shell.OpenInEditor(FileTypesFilePath);
    }

    /// <summary>Reads the file types file again, e.g. after editing it (REQ-06).</summary>
    [RelayCommand]
    private void ReloadFileTypes() => LoadFileTypes(FileTypesFilePath);

    /// <summary>Opens a previous scan result file in the result view (REQ-21a).</summary>
    [RelayCommand]
    private void OpenResultFile()
    {
        var path = _dialogs.PickFile("Open a scan result file", JsonFilter, Result?.ResultFilePath ?? Path.Join(OutputFolder, "x"));
        if (path is null)
        {
            return;
        }

        try
        {
            Result = new ScanResultViewModel(ScanResultFile.Load(path), path, _shell);
            StatusMessage = $"Opened {Path.GetFileName(path)}.";
        }
        catch (ScanResultFileException ex)
        {
            _dialogs.ShowError("Cannot open the scan result file", ex.Message);
        }
    }

    /// <summary>Validates the input, runs the scan with a progress dialog, saves and shows the result (REQ-12..REQ-20).</summary>
    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScanAsync()
    {
        StatusMessage = null;
        if (!TryPrepareScan(out var options))
        {
            return;
        }

        SaveSettings();

        using var progress = new ScanProgressViewModel(_time);
        var reporter = new Progress<ScanProgress>(progress.Update);
        ScanResult? result = null;
        try
        {
            await _dialogs.RunWithProgressAsync(
                progress,
                () => Task.Run(() => result = Scanner.Run(options, reporter, progress.CancellationToken)));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled. No result file was written.";
            return;
        }
        catch (ScanValidationException ex)
        {
            // The folders were valid a moment ago, but may have been removed since.
            _dialogs.ShowError("Cannot start the scan", ex.Message);
            return;
        }

        string resultPath;
        try
        {
            resultPath = ScanResultFile.Save(result!, OutputFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _dialogs.ShowError("Cannot save the result file", $"The result file could not be written to '{OutputFolder}': {ex.Message}");
            return;
        }

        Result = new ScanResultViewModel(result!, resultPath, _shell);
        StatusMessage = $"Scan finished: {result!.Summary.Missing:N0} missing files.";
    }

    private bool CanStartScan() =>
        !string.IsNullOrWhiteSpace(SourceFolder) && !string.IsNullOrWhiteSpace(DestinationFolder) && FileTypesError is null;

    private bool TryPrepareScan(out ScanOptions options)
    {
        options = null!;
        if (_fileTypes is null)
        {
            _dialogs.ShowError("File types file", FileTypesError ?? "No file types file is loaded.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            _dialogs.ShowError("Cannot start the scan", "Choose a folder for the result file.");
            return false;
        }

        try
        {
            var (source, destination) = ScanValidation.ValidateRoots(SourceFolder, DestinationFolder);
            var filter = _fileTypes.CreateFilter(Groups.Where(g => g.IsChecked).Select(g => g.Name));
            options = new ScanOptions
            {
                SourceRoot = source,
                DestinationRoot = destination,
                Filter = filter,
                FileTypesFilePath = FileTypesFilePath,
                TimeProvider = _time,
            };
            return true;
        }
        catch (ScanValidationException ex)
        {
            _dialogs.ShowError("Cannot start the scan", ex.Message);
        }
        catch (FileTypesException)
        {
            _dialogs.ShowError("Cannot start the scan", "No file types are selected. Tick at least one group that has extensions.");
        }

        return false;
    }

    private void LoadFileTypes(string path)
    {
        FileTypesFilePath = path;
        Groups.Clear();
        _fileTypes = null;
        try
        {
            _fileTypes = FileTypesFile.Load(path);
            foreach (var group in _fileTypes.Groups)
            {
                Groups.Add(new FileTypeGroupViewModel(group));
            }

            FileTypesError = null;
        }
        catch (FileTypesException ex)
        {
            FileTypesError = ex.Message + " Fix the file and press Reload, or choose another file.";
        }
    }

    private string EnsureUserFileTypes()
    {
        try
        {
            return FileTypesFile.EnsureUserFile(_userFileTypesPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Loading will then report the problem in the file types area.
            return _userFileTypesPath;
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
