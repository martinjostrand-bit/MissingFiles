using System.Text.Json.Serialization;

namespace MissingFiles.Core.Scanning;

/// <summary>
/// Result of a scan, written to the scan result file (spec REQ-19).
/// </summary>
public sealed class ScanResult
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>The only match mode in v1: same file name (case-insensitive) and same size (REQ-08).</summary>
    public const string NameAndSizeMatchMode = "NameAndSize";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Name and version of the application that wrote the file.</summary>
    public string Application { get; init; } = ProductInfo.DisplayName;

    /// <summary>Start time, local time with UTC offset.</summary>
    public required DateTimeOffset ScanStarted { get; init; }

    /// <summary>End time, local time with UTC offset.</summary>
    public required DateTimeOffset ScanFinished { get; init; }

    public required string SourceRoot { get; init; }

    public required string DestinationRoot { get; init; }

    public string MatchMode { get; init; } = NameAndSizeMatchMode;

    /// <summary>Path of the file types file used, or null if the built-in default was used.</summary>
    [JsonPropertyName("fileTypesFile")]
    public string? FileTypesFilePath { get; init; }

    /// <summary>The effective extensions compared (REQ-06a).</summary>
    public required IReadOnlyList<string> Extensions { get; init; }

    public required ScanSummary Summary { get; init; }

    /// <summary>Source files without a match, sorted by relative path.</summary>
    public required IReadOnlyList<MissingFile> MissingFiles { get; init; }

    /// <summary>Folders that could not be read (REQ-16).</summary>
    public required IReadOnlyList<ScanError> Errors { get; init; }

    [JsonIgnore]
    public TimeSpan Duration => ScanFinished - ScanStarted;
}

/// <summary>Counters of a scan (REQ-20).</summary>
/// <param name="SourceFilesScanned">Source files of the selected types.</param>
/// <param name="Matched">Source files with a match in the destination.</param>
/// <param name="Missing">Source files without a match.</param>
/// <param name="Errors">Folders that could not be read.</param>
/// <param name="DuplicateNameGroups">File names that occur more than once in the source (REQ-11).</param>
/// <param name="SameNameDifferentSize">Missing files for which the destination has a file with the same name but a different size (REQ-10).</param>
public sealed record ScanSummary(
    int SourceFilesScanned,
    int Matched,
    int Missing,
    int Errors,
    int DuplicateNameGroups,
    int SameNameDifferentSize);

/// <summary>A source file without a match in the destination.</summary>
/// <param name="RelativePath">Path relative to the source root.</param>
/// <param name="SizeBytes">File size.</param>
/// <param name="LastWriteTimeUtc">Last write time (UTC), used by the copy step to detect changed files (REQ-27).</param>
/// <param name="SameNameDifferentSize">The destination has a file with the same name but a different size.</param>
public sealed record MissingFile(string RelativePath, long SizeBytes, DateTime LastWriteTimeUtc, bool SameNameDifferentSize);

/// <summary>A folder that could not be read.</summary>
public sealed record ScanError(string Path, string Message);
