using MissingFiles.Core.Copying;
using MissingFiles.Core.Scanning;

namespace MissingFiles.Cli;

/// <summary>The <c>copy</c> command (spec REQ-33).</summary>
internal static class CopyCommand
{
    public const string Usage = """
        Usage: MissingFiles.Cli copy --result <file.json> [options]

        Copies the missing files listed in a scan result file from the source folder
        to the same relative path in the destination folder. Existing files are never
        overwritten. A copy log is written to the output folder.

        Options:
          --result <file>         Scan result file written by 'scan' (required).
          --out <folder>          Where to write the copy log.
                                  Default: Documents\MissingFiles
          --dry-run               Check and log what would be copied, without copying.

        Exit codes: 0 all files copied, 1 some files skipped, 2 some files failed,
                    3 invalid arguments or input (incl. not enough free space),
                    4 cancelled, 5 fatal error.
        """;

    private const int MaxFailuresShown = 20;

    public static ExitCode Run(IReadOnlyList<string> args, CliContext context)
    {
        var commandLine = CommandLine.Parse(
            args,
            valueOptions: ["result", "out"],
            flagOptions: ["dry-run"],
            requiredOptions: ["result"]);
        if (commandLine.Help)
        {
            context.Out.WriteLine(Usage);
            return ExitCode.Success;
        }

        var resultPath = Path.GetFullPath(commandLine.Require("result"));
        var dryRun = commandLine.Has("dry-run");
        var outputFolder = commandLine.Get("out") ?? context.DefaultOutputFolder;

        var scanResult = ScanResultFile.Load(resultPath);
        var preview = Copier.Preview(scanResult);
        WritePreview(context, resultPath, preview, dryRun);

        var line = new ProgressLine(context);
        var result = Copier.Run(
            new CopyOptions
            {
                ScanResult = scanResult,
                ScanResultFilePath = resultPath,
                DryRun = dryRun,
                TimeProvider = context.TimeProvider,
            },
            new SyncProgress<CopyProgress>(p => line.Update(Describe(p))),
            context.CancellationToken);
        line.Finish();

        var logPath = CopyLogFile.Save(result, outputFolder);
        WriteSummary(context, result, logPath);
        return ExitCodeFor(result);
    }

    /// <summary>Exit code for a finished copy (REQ-35).</summary>
    internal static ExitCode ExitCodeFor(CopyResult result) =>
        result.Cancelled ? ExitCode.Cancelled
        : result.Summary.Failed > 0 ? ExitCode.CompletedWithErrors
        : result.Summary.SkippedExists + result.Summary.SkippedSourceChanged > 0 ? ExitCode.SuccessWithFindings
        : ExitCode.Success;

    private static void WritePreview(CliContext context, string resultPath, CopyPreview preview, bool dryRun)
    {
        var output = context.Out;
        output.WriteLine(Format.Field("Scan result file", resultPath));
        output.WriteLine(Format.Field("Source", preview.SourceRoot));
        output.WriteLine(Format.Field("Destination", preview.DestinationRoot));
        output.WriteLine(Format.Field("Files to copy", $"{Format.Count(preview.FileCount)} ({Copier.FormatBytes(preview.TotalBytes)})"));
        output.WriteLine(Format.Field("Free space", preview.FreeBytes is { } free ? Copier.FormatBytes(free) : "unknown"));
        if (dryRun)
        {
            output.WriteLine("  DRY RUN: no files will be written.");
        }
    }

    private static string Describe(CopyProgress p)
    {
        var remaining = p.EstimatedRemaining is { } eta ? $", about {Format.Duration(eta)} left" : string.Empty;
        return $"Copying: {Format.Count(p.FilesProcessed)}/{Format.Count(p.TotalFiles)} files, "
            + $"{Copier.FormatBytes(p.BytesCopied)} of {Copier.FormatBytes(p.TotalBytes)}, "
            + $"{Format.Count(p.Skipped)} skipped, {Format.Count(p.Failed)} failed | {Format.Duration(p.Elapsed)}{remaining}";
    }

    private static void WriteSummary(CliContext context, CopyResult result, string logPath)
    {
        var s = result.Summary;
        var output = context.Out;

        output.WriteLine();
        output.WriteLine(result.Cancelled
            ? $"Copy cancelled after {Format.Duration(result.Duration)}"
            : $"Copy finished in {Format.Duration(result.Duration)}");
        output.WriteLine(Format.Field(result.DryRun ? "Would copy" : "Copied", Format.Count(s.Copied)));
        output.WriteLine(Format.Field("Skipped - exists", Format.Count(s.SkippedExists)));
        output.WriteLine(Format.Field("Skipped - source changed", Format.Count(s.SkippedSourceChanged)));
        output.WriteLine(Format.Field("Failed", Format.Count(s.Failed)));
        if (s.NotProcessed > 0)
        {
            output.WriteLine(Format.Field("Not processed", Format.Count(s.NotProcessed)));
        }

        output.WriteLine(Format.Field("Bytes copied", Copier.FormatBytes(s.BytesCopied)));
        output.WriteLine(Format.Field("Copy log", logPath));

        var failures = result.Entries.Where(e => e.Status == CopyStatus.Failed).ToList();
        if (failures.Count > 0)
        {
            context.Error.WriteLine($"{failures.Count} file(s) could not be copied:");
            foreach (var failure in failures.Take(MaxFailuresShown))
            {
                context.Error.WriteLine($"  {failure.RelativePath}: {failure.Message}");
            }

            if (failures.Count > MaxFailuresShown)
            {
                context.Error.WriteLine($"  ... and {failures.Count - MaxFailuresShown} more (see the copy log).");
            }
        }
    }
}
