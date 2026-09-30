using MissingFiles.App.Services;
using MissingFiles.App.ViewModels;

namespace MissingFiles.App.Tests;

/// <summary>A temporary folder with source, destination and output subfolders, deleted on dispose.</summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Root = Path.Join(Path.GetTempPath(), "MissingFilesAppTests", Guid.NewGuid().ToString("N"));
        Source = Directory.CreateDirectory(Path.Join(Root, "source")).FullName;
        Destination = Directory.CreateDirectory(Path.Join(Root, "destination")).FullName;
        Output = Path.Join(Root, "out");
        UserFileTypes = Path.Join(Root, "appdata", "FileTypes.json");
    }

    public string Root { get; }

    public string Source { get; }

    public string Destination { get; }

    public string Output { get; }

    public string UserFileTypes { get; }

    public string AddSource(string relativePath, int size = 10) => CreateFile(Source, relativePath, size);

    public string AddDestination(string relativePath, int size = 10) => CreateFile(Destination, relativePath, size);

    public string WriteFile(string relativePath, string content)
    {
        var path = Path.Join(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string[] OutputFiles() => Directory.Exists(Output) ? Directory.GetFiles(Output) : [];

    private static string CreateFile(string root, string relativePath, int size)
    {
        var path = Path.Join(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

/// <summary>Answers dialogs from queues and records what was shown.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public Queue<string?> FolderAnswers { get; } = new();

    public Queue<string?> FileAnswers { get; } = new();

    public List<(string Title, string Message)> Errors { get; } = [];

    public List<ProgressViewModel> ProgressDialogs { get; } = [];

    /// <summary>Called when the progress dialog is shown, before the work runs; e.g. to press Cancel.</summary>
    public Action<ProgressViewModel>? OnProgressShown { get; set; }

    public string? PickFolder(string title, string? initialFolder) => FolderAnswers.Dequeue();

    public string? PickFile(string title, string filter, string? initialFile) => FileAnswers.Dequeue();

    public void ShowError(string title, string message) => Errors.Add((title, message));

    public async Task RunWithProgressAsync(ProgressViewModel progress, Func<Task> work)
    {
        ProgressDialogs.Add(progress);
        OnProgressShown?.Invoke(progress);
        await work();
    }
}

internal sealed class FakeShellService : IShellService
{
    public List<string> OpenedInEditor { get; } = [];

    public List<string> ShownInExplorer { get; } = [];

    public void OpenInEditor(string path) => OpenedInEditor.Add(path);

    public void ShowInExplorer(string path) => ShownInExplorer.Add(path);
}

internal sealed class InMemorySettingsStore : ISettingsStore
{
    public AppSettings Settings { get; set; } = new();

    public int SaveCount { get; private set; }

    public AppSettings Load() => Settings;

    public void Save(AppSettings settings)
    {
        Settings = settings;
        SaveCount++;
    }
}
