using System.IO.Enumeration;
using System.Security;

using MissingFiles.Core.FileTypes;

namespace MissingFiles.Core.Scanning;

/// <summary>A file found by <see cref="FileWalker"/>.</summary>
internal readonly record struct FoundFile(string RelativeDirectory, string Name, long Length, DateTime LastWriteTimeUtc)
{
    public string RelativePath => Path.Join(RelativeDirectory, Name);
}

/// <summary>
/// Recursively lists the files of a folder that pass the extension filter (spec REQ-03, REQ-03a, REQ-07, REQ-16).
/// </summary>
/// <remarks>
/// Folders are read one at a time, so a folder that cannot be read is reported as an error
/// and the walk continues. Only directory metadata is read; no file is opened.
/// </remarks>
internal static class FileWalker
{
    /// <summary>Windows system folders that are never scanned (REQ-03a).</summary>
    private static readonly string[] ExcludedDirectoryNames = ["$Recycle.Bin", "System Volume Information", "$WinREAgent"];

    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        ReturnSpecialDirectories = false,
    };

    public static IEnumerable<FoundFile> EnumerateFiles(
        string root,
        ExtensionFilter filter,
        Action<ScanError> onError,
        CancellationToken cancellationToken)
    {
        var rootPrefixLength = ScanValidation.WithTrailingSeparator(root).Length;
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<Entry> entries;
            try
            {
                entries = ReadDirectory(directory, filter);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                onError(new ScanError(directory, ex.Message));
                continue;
            }

            var relativeDirectory = directory.Length > rootPrefixLength ? directory[rootPrefixLength..] : string.Empty;
            foreach (var entry in entries)
            {
                if (entry.IsDirectory)
                {
                    pending.Push(entry.NameOrPath);
                }
                else
                {
                    yield return new FoundFile(relativeDirectory, entry.NameOrPath, entry.Length, entry.LastWriteTimeUtc);
                }
            }
        }
    }

    private static List<Entry> ReadDirectory(string directory, ExtensionFilter filter)
    {
        var enumerable = new FileSystemEnumerable<Entry>(
            directory,
            static (ref FileSystemEntry e) => e.IsDirectory
                ? new Entry(true, e.ToFullPath(), 0, default)
                : new Entry(false, e.FileName.ToString(), e.Length, e.LastWriteTimeUtc.UtcDateTime),
            Options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry e) => e.IsDirectory
                ? IncludeDirectory(ref e)
                : filter.Matches(e.FileName),
        };

        return [.. enumerable];
    }

    private static bool IncludeDirectory(ref FileSystemEntry entry)
    {
        // Junctions and symbolic links are not followed (REQ-07): they can point outside
        // the root or back to a parent folder and cause an endless loop.
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return false;
        }

        foreach (var excluded in ExcludedDirectoryNames)
        {
            if (entry.FileName.Equals(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A directory (full path) or a file (name only).</summary>
    private readonly record struct Entry(bool IsDirectory, string NameOrPath, long Length, DateTime LastWriteTimeUtc);
}
