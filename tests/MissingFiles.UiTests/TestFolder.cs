namespace MissingFiles.UiTests;

/// <summary>
/// A temporary folder with source, destination and output subfolders, and a data folder
/// for the app, so that it does not touch the real settings. Deleted on dispose.
/// </summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Root = Path.Join(Path.GetTempPath(), "MissingFilesUiTests", Guid.NewGuid().ToString("N"));
        Source = Directory.CreateDirectory(Path.Join(Root, "source")).FullName;
        Destination = Directory.CreateDirectory(Path.Join(Root, "destination")).FullName;
        Output = Path.Join(Root, "out");
        DataFolder = Path.Join(Root, "appdata");
        UserFileTypes = Path.Join(DataFolder, "FileTypes.json");
    }

    public string Root { get; }

    public string Source { get; }

    public string Destination { get; }

    /// <summary>Output folder for result files and logs; not created in advance.</summary>
    public string Output { get; }

    /// <summary>Stands in for <c>%APPDATA%\MissingFiles</c>; not created in advance.</summary>
    public string DataFolder { get; }

    /// <summary>Stands in for <c>%APPDATA%\MissingFiles\FileTypes.json</c>; not created in advance.</summary>
    public string UserFileTypes { get; }

    public string AddSource(string relativePath, int size = 10) => CreateFile(Source, relativePath, size);

    public string AddDestination(string relativePath, int size = 10) => CreateFile(Destination, relativePath, size);

    public void WriteUserFileTypes(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UserFileTypes)!);
        File.WriteAllText(UserFileTypes, content);
    }

    public string[] OutputFiles(string pattern) =>
        Directory.Exists(Output) ? Directory.GetFiles(Output, pattern) : [];

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
