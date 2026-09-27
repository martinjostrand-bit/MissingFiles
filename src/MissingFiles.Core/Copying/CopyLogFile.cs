using System.Globalization;
using System.Text;

namespace MissingFiles.Core.Copying;

/// <summary>
/// Writes the copy log (spec REQ-32): header, summary, then one line per file.
/// </summary>
public static class CopyLogFile
{
    public const string FileNamePrefix = "MissingFilesCopy_";

    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Saves the log as <c>MissingFilesCopy_YYYYMMDD_HHMMSS.log</c> (copy start, local time).
    /// If that name exists, <c>_2</c>, <c>_3</c>, ... is appended. Existing files are never overwritten.
    /// </summary>
    /// <returns>The full path of the written file.</returns>
    public static string Save(CopyResult result, string outputFolder)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);

        Directory.CreateDirectory(outputFolder);
        var baseName = FileNamePrefix + result.Started.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var content = Format(result);

        for (var attempt = 1; ; attempt++)
        {
            var fileName = attempt == 1 ? $"{baseName}.log" : $"{baseName}_{attempt}.log";
            var path = Path.GetFullPath(Path.Join(outputFolder, fileName));
            if (File.Exists(path))
            {
                continue;
            }

            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            writer.Write(content);
            return path;
        }
    }

    /// <summary>Formats the log text.</summary>
    public static string Format(CopyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var c = CultureInfo.InvariantCulture;
        var s = result.Summary;
        var text = new StringBuilder();

        text.AppendLine(result.DryRun ? "MissingFiles copy log (DRY RUN - no files were written)" : "MissingFiles copy log");
        text.AppendLine();

        text.AppendLine("[Header]");
        AppendField(text, "Application", ProductInfo.DisplayName);
        AppendField(text, "Started", result.Started.ToString(TimeFormat + " zzz", c));
        AppendField(text, "Finished", result.Finished.ToString(TimeFormat + " zzz", c));
        AppendField(text, "Scan result file", result.ScanResultFilePath ?? "(not saved)");
        AppendField(text, "Source folder", result.SourceRoot);
        AppendField(text, "Destination folder", result.DestinationRoot);
        AppendField(text, "Mode", result.DryRun ? "Dry run" : "Copy");
        AppendField(text, "User", $"{Environment.UserDomainName}\\{Environment.UserName}");
        AppendField(text, "Machine", Environment.MachineName);
        text.AppendLine();

        text.AppendLine("[Summary]");
        AppendField(text, "Files in scan result", s.Total.ToString(c));
        AppendField(text, result.DryRun ? "Would copy" : "Copied", s.Copied.ToString(c));
        AppendField(text, "Skipped - exists", s.SkippedExists.ToString(c));
        AppendField(text, "Skipped - source changed", s.SkippedSourceChanged.ToString(c));
        AppendField(text, "Failed", s.Failed.ToString(c));
        AppendField(text, "Not processed", s.NotProcessed.ToString(c));
        AppendField(text, "Cancelled", result.Cancelled ? "Yes" : "No");
        AppendField(text, "Bytes copied", string.Create(c, $"{s.BytesCopied} ({Copier.FormatBytes(s.BytesCopied)})"));
        AppendField(text, "Elapsed", result.Duration.ToString(@"hh\:mm\:ss", c));
        text.AppendLine();

        text.AppendLine("[Details]");
        text.AppendLine("Time                | Status                   | Relative path | Size (bytes) | Message");
        foreach (var entry in result.Entries)
        {
            text.Append(entry.Time.ToString(TimeFormat, c))
                .Append(" | ").Append(StatusText(entry.Status).PadRight(24))
                .Append(" | ").Append(entry.RelativePath)
                .Append(" | ").Append(entry.SizeBytes.ToString(c))
                .Append(" | ").AppendLine(entry.Message);
        }

        return text.ToString();
    }

    /// <summary>Text shown for a status in the log and the UI.</summary>
    public static string StatusText(CopyStatus status) => status switch
    {
        CopyStatus.Copied => "Copied",
        CopyStatus.WouldCopy => "Would copy",
        CopyStatus.SkippedExists => "Skipped - exists",
        CopyStatus.SkippedSourceChanged => "Skipped - source changed",
        CopyStatus.Failed => "Failed",
        _ => status.ToString(),
    };

    private static void AppendField(StringBuilder text, string name, string value) =>
        text.Append((name + ":").PadRight(27)).AppendLine(value);
}
