namespace MissingFiles.Cli;

/// <summary>
/// Process exit codes (spec REQ-35).
/// </summary>
public enum ExitCode
{
    /// <summary>Success: no missing files / all files copied.</summary>
    Success = 0,

    /// <summary>Success: missing files found / some files skipped.</summary>
    SuccessWithFindings = 1,

    /// <summary>Completed with file errors: folders that could not be read / files that could not be copied.</summary>
    CompletedWithErrors = 2,

    /// <summary>
    /// Invalid arguments, or input that prevents starting: missing or overlapping folders,
    /// an invalid file types or scan result file, not enough free space.
    /// </summary>
    InvalidArguments = 3,

    /// <summary>Cancelled by the user.</summary>
    Cancelled = 4,

    /// <summary>Fatal error.</summary>
    FatalError = 5,
}
