using MissingFiles.Core.FileTypes;
using MissingFiles.Core.Scanning;

namespace MissingFiles.Cli;

/// <summary>
/// Everything a command needs from its environment; replaced in tests.
/// </summary>
internal sealed class CliContext(TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
{
    public TextWriter Out { get; } = output;

    public TextWriter Error { get; } = error;

    /// <summary>Signalled by Ctrl+C (REQ-34).</summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;

    /// <summary>True when stdout is a console window: progress is shown on one updating line.</summary>
    public bool Interactive { get; init; }

    /// <summary>File types file used when <c>--types-file</c> is not given (REQ-33a).</summary>
    public string UserFileTypesPath { get; init; } = FileTypesFile.UserFilePath;

    /// <summary>Folder for result files and logs when <c>--out</c> is not given (REQ-18).</summary>
    public string DefaultOutputFolder { get; init; } = ScanResultFile.DefaultOutputFolder;

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
