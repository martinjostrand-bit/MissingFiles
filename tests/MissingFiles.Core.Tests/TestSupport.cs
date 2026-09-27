namespace MissingFiles.Core.Tests;

/// <summary>A temporary folder with source and destination subfolders, deleted on dispose.</summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "MissingFilesTests", Guid.NewGuid().ToString("N"));
        Source = Path.Combine(Root, "source");
        Destination = Path.Combine(Root, "destination");
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(Destination);
    }

    public string Root { get; }

    public string Source { get; }

    public string Destination { get; }

    /// <summary>Creates a file of the given size under the source folder.</summary>
    public string AddSource(string relativePath, int size = 10) => CreateFile(Source, relativePath, size);

    /// <summary>Creates a file of the given size under the destination folder.</summary>
    public string AddDestination(string relativePath, int size = 10) => CreateFile(Destination, relativePath, size);

    /// <summary>Creates the same file (path and size) in source and destination.</summary>
    public void AddBoth(string relativePath, int size = 10)
    {
        AddSource(relativePath, size);
        AddDestination(relativePath, size);
    }

    public static string CreateFile(string root, string relativePath, int size = 10)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        // Clear attributes set by tests (hidden, read-only) so the folder can be deleted.
        foreach (var entry in Directory.EnumerateFileSystemEntries(Root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
        {
            File.SetAttributes(entry, File.GetAttributes(entry) & FileAttributes.Directory);
        }

        Directory.Delete(Root, recursive: true);
    }
}

/// <summary>A clock that always returns the same time.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("Test", now.Offset, "Test", "Test");
}

/// <summary>Collects progress reports synchronously (unlike <see cref="Progress{T}"/>).</summary>
internal sealed class ProgressRecorder<T> : IProgress<T>
{
    public List<T> Reports { get; } = [];

    public void Report(T value) => Reports.Add(value);
}
