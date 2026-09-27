namespace MissingFiles.Core.Scanning;

public enum ScanPhase
{
    /// <summary>Reading the destination folder.</summary>
    IndexingDestination,

    /// <summary>Reading the source folder and matching against the destination.</summary>
    ScanningSource,

    /// <summary>The scan has finished.</summary>
    Completed,
}

/// <summary>Progress of a running scan, shown in the progress dialog (REQ-13).</summary>
public sealed record ScanProgress(
    ScanPhase Phase,
    int DestinationFilesIndexed,
    int SourceFilesScanned,
    int Matched,
    int Missing,
    int Errors,
    TimeSpan Elapsed);
