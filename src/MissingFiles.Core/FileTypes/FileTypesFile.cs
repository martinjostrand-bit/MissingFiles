using System.Text.Json;

namespace MissingFiles.Core.FileTypes;

/// <summary>
/// Reads and validates file types files (spec §4.2).
/// </summary>
public static class FileTypesFile
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>File name of the default file shipped next to the executables.</summary>
    public const string DefaultFileName = "FileTypes.default.json";

    private const string DefaultResourceName = "MissingFiles.Core.FileTypes.default.json";
    private const string DefaultSource = "(built-in default)";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The user's editable file: <c>%APPDATA%\MissingFiles\FileTypes.json</c> (REQ-05b).</summary>
    public static string UserFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MissingFiles", "FileTypes.json");

    /// <summary>Reads and validates a file types file.</summary>
    /// <exception cref="FileTypesException">The file cannot be read or is not valid.</exception>
    public static FileTypesDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw FileTypesException.For(path, $"The file could not be read: {ex.Message}", ex);
        }

        return Parse(json, path);
    }

    /// <summary>Returns the built-in default definition (pictures and videos).</summary>
    public static FileTypesDefinition LoadDefault() => Parse(ReadDefaultJson(), DefaultSource);

    /// <summary>
    /// Creates <see cref="UserFilePath"/> from the built-in default if it does not exist (REQ-05b).
    /// An existing file is never changed.
    /// </summary>
    /// <returns>The path of the user file.</returns>
    public static string EnsureUserFile() => EnsureUserFile(UserFilePath);

    /// <inheritdoc cref="EnsureUserFile()"/>
    /// <param name="path">Path of the user file.</param>
    public static string EnsureUserFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, ReadDefaultJson());
        }

        return path;
    }

    /// <summary>Parses and validates the JSON content of a file types file.</summary>
    /// <param name="json">The file content.</param>
    /// <param name="fileName">File path (or description) used in error messages.</param>
    /// <exception cref="FileTypesException">The content is not valid.</exception>
    public static FileTypesDefinition Parse(string json, string fileName)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(fileName);

        FileTypesDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<FileTypesDocument>(json, ReadOptions);
        }
        catch (JsonException ex)
        {
            throw FileTypesException.For(fileName, JsonErrors.Describe(ex), ex);
        }

        if (document is null)
        {
            throw FileTypesException.For(fileName, "The file does not contain a JSON object.");
        }

        if (document.SchemaVersion is null)
        {
            throw FileTypesException.For(fileName, "'schemaVersion' is missing.");
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw FileTypesException.For(
                fileName,
                $"schemaVersion {document.SchemaVersion} is not supported. This version of MissingFiles supports schemaVersion {CurrentSchemaVersion}.");
        }

        if (document.Groups is null || document.Groups.Count == 0)
        {
            throw FileTypesException.For(fileName, "'groups' is missing or empty.");
        }

        var groups = new List<FileTypeGroup>(document.Groups.Count);
        for (var i = 0; i < document.Groups.Count; i++)
        {
            groups.Add(ToGroup(document.Groups[i], i + 1, fileName));
        }

        var duplicate = groups
            .GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw FileTypesException.For(fileName, $"Group name '{duplicate.Key}' is used more than once.");
        }

        return new FileTypesDefinition(fileName, groups);
    }

    private static FileTypeGroup ToGroup(GroupDocument? group, int number, string fileName)
    {
        if (group is null)
        {
            throw FileTypesException.For(fileName, $"Group {number} is empty.");
        }

        if (string.IsNullOrWhiteSpace(group.Name))
        {
            throw FileTypesException.For(fileName, $"Group {number} has no 'name'.");
        }

        var name = group.Name.Trim();
        if (group.Extensions is null)
        {
            throw FileTypesException.For(fileName, $"Group '{name}' has no 'extensions' list.");
        }

        var extensions = new List<string>(group.Extensions.Count);
        foreach (var extension in group.Extensions)
        {
            try
            {
                extensions.Add(ExtensionFilter.Normalize(extension));
            }
            catch (ArgumentException ex)
            {
                var problem = ex.ParamName is null
                    ? ex.Message
                    : ex.Message.Replace($" (Parameter '{ex.ParamName}')", string.Empty, StringComparison.Ordinal);
                throw FileTypesException.For(fileName, $"Group '{name}': {problem}", ex);
            }
        }

        // A missing "enabled" means enabled, so a minimal group only needs a name and extensions.
        return new FileTypeGroup(name, group.Enabled ?? true, extensions.Distinct().ToList());
    }

    private static string ReadDefaultJson()
    {
        using var stream = typeof(FileTypesFile).Assembly.GetManifestResourceStream(DefaultResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {DefaultResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class FileTypesDocument
    {
        public int? SchemaVersion { get; set; }

        public List<GroupDocument?>? Groups { get; set; }
    }

    private sealed class GroupDocument
    {
        public string? Name { get; set; }

        public bool? Enabled { get; set; }

        public List<string?>? Extensions { get; set; }
    }
}
