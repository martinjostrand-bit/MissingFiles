using MissingFiles.Core.FileTypes;

namespace MissingFiles.Core.Scanning;

/// <summary>Input for <see cref="Scanner.Run"/>.</summary>
public sealed class ScanOptions
{
    public required string SourceRoot { get; init; }

    public required string DestinationRoot { get; init; }

    /// <summary>Which files take part in the scan.</summary>
    public required ExtensionFilter Filter { get; init; }

    /// <summary>Path of the file types file, recorded in the result (REQ-06a). Null for the built-in default.</summary>
    public string? FileTypesFilePath { get; init; }

    /// <summary>Clock used for timestamps and progress; replaceable in tests.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
