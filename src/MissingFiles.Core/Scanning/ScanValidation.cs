namespace MissingFiles.Core.Scanning;

/// <summary>
/// Checks the source and destination folders before a scan (spec REQ-02).
/// </summary>
public static class ScanValidation
{
    /// <summary>
    /// Validates both folders and returns them as full paths without a trailing separator
    /// (except for drive roots such as <c>D:\</c>).
    /// </summary>
    /// <exception cref="ScanValidationException">A folder is missing, not readable, or the folders overlap.</exception>
    public static (string SourceRoot, string DestinationRoot) ValidateRoots(string sourceRoot, string destinationRoot)
    {
        var source = ValidateFolder(sourceRoot, "Source folder");
        var destination = ValidateFolder(destinationRoot, "Destination folder");

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new ScanValidationException($"Source and destination are the same folder: {source}");
        }

        if (IsInside(destination, source))
        {
            throw new ScanValidationException($"The destination folder is inside the source folder: {destination}");
        }

        if (IsInside(source, destination))
        {
            throw new ScanValidationException($"The source folder is inside the destination folder: {source}");
        }

        return (source, destination);
    }

    /// <summary>Returns the path with exactly one trailing directory separator.</summary>
    internal static string WithTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    private static bool IsInside(string path, string folder) =>
        path.StartsWith(WithTrailingSeparator(folder), StringComparison.OrdinalIgnoreCase);

    private static string ValidateFolder(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ScanValidationException($"{label} is not specified.");
        }

        string fullPath;
        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ScanValidationException($"{label} is not a valid path: {path}", ex);
        }

        if (!Directory.Exists(fullPath))
        {
            throw new ScanValidationException($"{label} does not exist: {fullPath}");
        }

        try
        {
            using var entries = Directory.EnumerateFileSystemEntries(fullPath).GetEnumerator();
            entries.MoveNext();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new ScanValidationException($"{label} cannot be read: {fullPath} ({ex.Message})", ex);
        }

        return fullPath;
    }
}
