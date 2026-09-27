namespace MissingFiles.Core.FileTypes;

/// <summary>A named group of extensions from the file types file.</summary>
/// <param name="Name">Group name, e.g. "Pictures".</param>
/// <param name="Enabled">Whether the group is used unless overridden.</param>
/// <param name="Extensions">Normalized extensions (see <see cref="ExtensionFilter.Normalize"/>).</param>
public sealed record FileTypeGroup(string Name, bool Enabled, IReadOnlyList<string> Extensions);

/// <summary>The validated content of a file types file.</summary>
public sealed class FileTypesDefinition
{
    internal FileTypesDefinition(string fileName, IReadOnlyList<FileTypeGroup> groups)
    {
        FileName = fileName;
        Groups = groups;
    }

    /// <summary>Path of the file the definition was read from, or a description of the built-in default.</summary>
    public string FileName { get; }

    public IReadOnlyList<FileTypeGroup> Groups { get; }

    /// <summary>
    /// Creates the filter for a scan.
    /// </summary>
    /// <param name="enabledGroups">
    /// Names of the groups to use (case-insensitive), overriding the <c>enabled</c> flags in the file
    /// (UI checkboxes, CLI <c>--groups</c>). Null uses the flags from the file.
    /// </param>
    /// <exception cref="FileTypesException">An unknown group name was given, or no extensions are enabled.</exception>
    public ExtensionFilter CreateFilter(IEnumerable<string>? enabledGroups = null)
    {
        IEnumerable<FileTypeGroup> selected;
        if (enabledGroups is null)
        {
            selected = Groups.Where(g => g.Enabled);
        }
        else
        {
            var names = new HashSet<string>(enabledGroups.Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);
            var unknown = names
                .Where(n => !Groups.Any(g => string.Equals(g.Name, n, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (unknown.Count > 0)
            {
                throw FileTypesException.For(
                    FileName,
                    $"Unknown group(s): {string.Join(", ", unknown)}. Available groups: {string.Join(", ", Groups.Select(g => g.Name))}.");
            }

            selected = Groups.Where(g => names.Contains(g.Name));
        }

        var extensions = selected.SelectMany(g => g.Extensions).ToList();
        if (extensions.Count == 0)
        {
            throw FileTypesException.For(FileName, "No file types are enabled. Enable at least one group that has extensions.");
        }

        return new ExtensionFilter(extensions);
    }
}
