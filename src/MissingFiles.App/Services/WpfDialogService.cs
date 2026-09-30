using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

using Microsoft.Win32;

using MissingFiles.App.ViewModels;
using MissingFiles.App.Views;

namespace MissingFiles.App.Services;

public sealed class WpfDialogService : IDialogService
{
    public string? PickFolder(string title, string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public string? PickFile(string title, string filter, string? initialFile)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        var folder = Path.GetDirectoryName(initialFile);
        if (Directory.Exists(folder))
        {
            dialog.InitialDirectory = folder;
        }

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public void ShowError(string title, string message)
    {
        // MessageBox.Show throws for a null owner, e.g. before the main window is shown.
        if (Owner is { } owner)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public async Task RunWithProgressAsync(ProgressViewModel progress, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        var window = new ProgressWindow { DataContext = progress, Owner = Owner };
        var task = work();

        // Close the dialog when the work ends. The continuation runs on the UI thread,
        // inside the message loop of ShowDialog, so it cannot run before the dialog is shown.
        _ = task.ContinueWith(
            _ => window.CloseWhenDone(),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());

        window.ShowDialog();
        await task.ConfigureAwait(true);
    }

    private static Window? Owner => Application.Current?.MainWindow;
}

public sealed class ShellService : IShellService
{
    public void OpenInEditor(string path)
    {
        try
        {
            // The program associated with .json files, if any ...
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "open" })?.Dispose();
        }
        catch (Win32Exception)
        {
            // ... otherwise Notepad, which is always available.
            Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { path } })?.Dispose();
        }
    }

    // Explorer expects the quotes around the path only: /select,"C:\a b\file.json".
    public void ShowInExplorer(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\""))?.Dispose();
}
