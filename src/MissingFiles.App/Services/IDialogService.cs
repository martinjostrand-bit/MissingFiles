using MissingFiles.App.ViewModels;

namespace MissingFiles.App.Services;

/// <summary>Dialogs shown by view models; replaced in tests.</summary>
public interface IDialogService
{
    /// <summary>Lets the user choose a folder; null if cancelled.</summary>
    string? PickFolder(string title, string? initialFolder);

    /// <summary>Lets the user choose an existing file; null if cancelled.</summary>
    string? PickFile(string title, string filter, string? initialFile);

    void ShowError(string title, string message);

    /// <summary>
    /// Shows a modal progress dialog while <paramref name="work"/> runs and closes it when the work ends.
    /// Exceptions of the work are rethrown.
    /// </summary>
    Task RunWithProgressAsync(ProgressViewModel progress, Func<Task> work);
}

/// <summary>Opens files and folders in other programs; replaced in tests.</summary>
public interface IShellService
{
    /// <summary>Opens a text file in the default editor (REQ-06).</summary>
    void OpenInEditor(string path);

    /// <summary>Opens Explorer with the file selected (REQ-20).</summary>
    void ShowInExplorer(string path);
}
