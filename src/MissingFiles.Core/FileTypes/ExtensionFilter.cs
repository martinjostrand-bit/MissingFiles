using System.Buffers;

namespace MissingFiles.Core.FileTypes;

/// <summary>
/// Decides which files take part in a scan, based on their extension (spec §4.2).
/// </summary>
public sealed class ExtensionFilter
{
    /// <summary>Matches all files, regardless of extension.</summary>
    public const string AllFiles = "*";

    /// <summary>Matches files without an extension.</summary>
    public const string NoExtension = "";

    private static readonly SearchValues<char> InvalidCharacters =
        SearchValues.Create([.. Path.GetInvalidFileNameChars(), '*', '?', ' ']);

    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    /// <summary>Creates a filter for the given extensions.</summary>
    /// <param name="extensions">Extensions in any accepted spelling (see <see cref="Normalize"/>).</param>
    /// <exception cref="ArgumentException">An extension is invalid, or the list is empty.</exception>
    public ExtensionFilter(IEnumerable<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in extensions)
        {
            set.Add(Normalize(extension));
        }

        if (set.Count == 0)
        {
            throw new ArgumentException("At least one extension is required.", nameof(extensions));
        }

        // Lookup by span, so files that are filtered out cost no string allocation.
        _lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        IncludesAllFiles = set.Contains(AllFiles);
        Extensions = [.. set.Order(StringComparer.Ordinal)];
    }

    /// <summary>True if the filter contains <see cref="AllFiles"/>.</summary>
    public bool IncludesAllFiles { get; }

    /// <summary>The effective, normalized extensions, sorted.</summary>
    public IReadOnlyList<string> Extensions { get; }

    /// <summary>Returns true if a file with this name takes part in the scan.</summary>
    public bool Matches(ReadOnlySpan<char> fileName) =>
        IncludesAllFiles || _lookup.Contains(Path.GetExtension(fileName));

    /// <summary>
    /// Normalizes an extension: trimmed, lower case, with a leading dot
    /// (<c>JPG</c>, <c>jpg</c> and <c>.Jpg</c> all become <c>.jpg</c>).
    /// <see cref="AllFiles"/> and <see cref="NoExtension"/> are returned unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">The extension is not valid.</exception>
    public static string Normalize(string? extension)
    {
        if (extension is null)
        {
            throw new ArgumentException("An extension must not be null.", nameof(extension));
        }

        var trimmed = extension.Trim();
        if (trimmed is AllFiles or NoExtension)
        {
            return trimmed;
        }

        var normalized = (trimmed.StartsWith('.') ? trimmed : "." + trimmed).ToLowerInvariant();

        if (normalized.Length == 1)
        {
            throw new ArgumentException($"'{extension}' is not a valid extension.", nameof(extension));
        }

        if (normalized.IndexOf('.', 1) >= 0)
        {
            throw new ArgumentException(
                $"'{extension}' is not a valid extension: only the part after the last dot counts (e.g. '.gz', not '.tar.gz').",
                nameof(extension));
        }

        if (normalized.AsSpan(1).IndexOfAny(InvalidCharacters) >= 0)
        {
            throw new ArgumentException(
                $"'{extension}' is not a valid extension: wildcards, spaces and path characters are not allowed (use \"*\" alone for all files).",
                nameof(extension));
        }

        return normalized;
    }
}
