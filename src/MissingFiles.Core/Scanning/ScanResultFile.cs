using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MissingFiles.Core.Scanning;

/// <summary>
/// Writes and reads scan result files (spec REQ-18, REQ-19).
/// </summary>
public static class ScanResultFile
{
    public const string FileNamePrefix = "MissingFiles_";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,

        // The file is read by people and tools, not embedded in HTML: keep non-ASCII
        // file names (e.g. "Bjørn.jpg") readable instead of escaping them as \uXXXX.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Default output folder: <c>Documents\MissingFiles</c> (REQ-18).</summary>
    public static string DefaultOutputFolder { get; } = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MissingFiles");

    /// <summary>
    /// Saves the result as <c>MissingFiles_YYYYMMDD_HHMMSS.json</c> (scan start, local time) in the output folder.
    /// If that name exists, <c>_2</c>, <c>_3</c>, ... is appended. Existing files are never overwritten.
    /// </summary>
    /// <returns>The full path of the written file.</returns>
    public static string Save(ScanResult result, string outputFolder)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);

        Directory.CreateDirectory(outputFolder);
        var baseName = FileNamePrefix + result.ScanStarted.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        for (var attempt = 1; ; attempt++)
        {
            var fileName = attempt == 1 ? $"{baseName}.json" : $"{baseName}_{attempt}.json";
            var path = Path.GetFullPath(Path.Join(outputFolder, fileName));
            if (File.Exists(path))
            {
                continue;
            }

            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, result, JsonOptions);
            return path;
        }
    }

    /// <summary>Reads a scan result file.</summary>
    /// <exception cref="ScanResultFileException">The file cannot be read or is not a valid scan result file.</exception>
    public static ScanResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        ScanResult? result;
        try
        {
            using var stream = File.OpenRead(path);
            result = JsonSerializer.Deserialize<ScanResult>(stream, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ScanResultFileException($"Scan result file '{path}': {JsonErrors.Describe(ex)}", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new ScanResultFileException($"Scan result file '{path}' could not be read: {ex.Message}", ex);
        }

        if (result is null)
        {
            throw new ScanResultFileException($"Scan result file '{path}' does not contain a JSON object.");
        }

        if (result.SchemaVersion != ScanResult.CurrentSchemaVersion)
        {
            throw new ScanResultFileException(
                $"Scan result file '{path}': schemaVersion {result.SchemaVersion} is not supported. This version of MissingFiles supports schemaVersion {ScanResult.CurrentSchemaVersion}.");
        }

        return result;
    }
}
