using MissingFiles.Core.Scanning;

namespace MissingFiles.Core.Copying;

/// <summary>Input for <see cref="Copier.Run"/>.</summary>
public sealed class CopyOptions
{
    /// <summary>The scan result listing the files to copy.</summary>
    public required ScanResult ScanResult { get; init; }

    /// <summary>Path of the scan result file, recorded in the copy log header.</summary>
    public string? ScanResultFilePath { get; init; }

    /// <summary>Only check and log what would be copied; write nothing (CLI <c>--dry-run</c>).</summary>
    public bool DryRun { get; init; }

    /// <summary>Clock used for timestamps and progress; replaceable in tests.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>Returns free bytes for a folder, or null if unknown. Replaceable in tests.</summary>
    internal Func<string, long?> FreeSpaceProvider { get; init; } = DiskSpace.GetAvailableFreeBytes;
}

/// <summary>What a copy would do, shown before the copy starts (REQ-23).</summary>
/// <param name="SourceRoot">Validated source folder.</param>
/// <param name="DestinationRoot">Validated destination folder.</param>
/// <param name="FileCount">Number of files in the scan result.</param>
/// <param name="TotalBytes">Total size of those files.</param>
/// <param name="FreeBytes">Free space on the destination drive, or null if it cannot be determined.</param>
public sealed record CopyPreview(string SourceRoot, string DestinationRoot, int FileCount, long TotalBytes, long? FreeBytes)
{
    /// <summary>True if there is enough free space, or if the free space is unknown.</summary>
    public bool HasEnoughSpace => FreeBytes is not { } free || free >= TotalBytes;
}

public enum CopyStatus
{
    /// <summary>The file was copied.</summary>
    Copied,

    /// <summary>Dry run: the file would have been copied.</summary>
    WouldCopy,

    /// <summary>The destination path already exists; nothing was overwritten (REQ-26).</summary>
    SkippedExists,

    /// <summary>The source file is gone or has changed since the scan (REQ-27).</summary>
    SkippedSourceChanged,

    /// <summary>The copy failed (REQ-29).</summary>
    Failed,
}

/// <summary>One line of the copy log details (REQ-32).</summary>
public sealed record CopyEntry(DateTimeOffset Time, CopyStatus Status, string RelativePath, long SizeBytes, string? Message);

/// <summary>Counters of a copy run (REQ-31, REQ-32).</summary>
/// <param name="Total">Files in the scan result.</param>
/// <param name="Copied">Files copied (in a dry run: files that would be copied).</param>
/// <param name="SkippedExists">Files skipped because the destination path exists.</param>
/// <param name="SkippedSourceChanged">Files skipped because the source is gone or changed.</param>
/// <param name="Failed">Files that could not be copied.</param>
/// <param name="NotProcessed">Files not reached because the copy was cancelled.</param>
/// <param name="BytesCopied">Bytes written to the destination.</param>
public sealed record CopySummary(
    int Total,
    int Copied,
    int SkippedExists,
    int SkippedSourceChanged,
    int Failed,
    int NotProcessed,
    long BytesCopied);

/// <summary>Result of a copy run.</summary>
public sealed class CopyResult
{
    public required DateTimeOffset Started { get; init; }

    public required DateTimeOffset Finished { get; init; }

    public string? ScanResultFilePath { get; init; }

    public required string SourceRoot { get; init; }

    public required string DestinationRoot { get; init; }

    public required bool DryRun { get; init; }

    public required bool Cancelled { get; init; }

    public required CopySummary Summary { get; init; }

    /// <summary>One entry per processed file, in processing order.</summary>
    public required IReadOnlyList<CopyEntry> Entries { get; init; }

    public TimeSpan Duration => Finished - Started;
}

/// <summary>Progress of a running copy, shown in the progress dialog (REQ-30).</summary>
/// <param name="FilesProcessed">Files handled so far (copied, skipped or failed).</param>
/// <param name="TotalFiles">Files in the scan result.</param>
/// <param name="Copied">Files copied so far.</param>
/// <param name="Skipped">Files skipped so far.</param>
/// <param name="Failed">Files failed so far.</param>
/// <param name="BytesCopied">Bytes written so far.</param>
/// <param name="TotalBytes">Total size of all files in the scan result.</param>
/// <param name="Elapsed">Time since the copy started.</param>
/// <param name="EstimatedRemaining">Estimated time left, or null while unknown.</param>
/// <param name="CurrentFile">Relative path of the file being copied, or null when done.</param>
public sealed record CopyProgress(
    int FilesProcessed,
    int TotalFiles,
    int Copied,
    int Skipped,
    int Failed,
    long BytesCopied,
    long TotalBytes,
    TimeSpan Elapsed,
    TimeSpan? EstimatedRemaining,
    string? CurrentFile);
