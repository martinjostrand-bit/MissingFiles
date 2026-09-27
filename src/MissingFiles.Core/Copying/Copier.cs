using System.Buffers;
using System.Security;

using MissingFiles.Core.Scanning;

namespace MissingFiles.Core.Copying;

/// <summary>
/// Copies the missing files of a scan result to the destination (spec §4.7).
/// </summary>
/// <remarks>
/// Safety rules: the source is only read (NFR-06); an existing destination path is never
/// overwritten (REQ-26); a file is written under a temporary name and renamed when complete,
/// so an aborted copy never leaves a partial file under the real name (REQ-28).
/// </remarks>
public static class Copier
{
    /// <summary>Suffix of temporary files; they are deleted again when a copy fails.</summary>
    internal const string TempSuffix = ".missingfiles-tmp";

    private const int BufferSize = 1024 * 1024;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Checks the folders and free space before a copy (REQ-23).</summary>
    /// <exception cref="CopyValidationException">A folder is missing or the folders overlap.</exception>
    public static CopyPreview Preview(ScanResult scanResult) => Preview(scanResult, DiskSpace.GetAvailableFreeBytes);

    /// <summary>
    /// Copies the files. The method is synchronous; UI callers should run it on a background thread.
    /// Cancellation stops after the current file and returns a result with <see cref="CopyResult.Cancelled"/> set.
    /// </summary>
    /// <exception cref="CopyValidationException">A folder is missing, the folders overlap, or there is not enough free space.</exception>
    public static CopyResult Run(
        CopyOptions options,
        IProgress<CopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var preview = Preview(options.ScanResult, options.FreeSpaceProvider);
        if (!options.DryRun && !preview.HasEnoughSpace)
        {
            throw new CopyValidationException(
                $"Not enough free space on the destination drive: {FormatBytes(preview.TotalBytes)} needed, {FormatBytes(preview.FreeBytes!.Value)} available.");
        }

        var time = options.TimeProvider;
        var started = time.GetLocalNow();
        var files = options.ScanResult.MissingFiles;
        var run = new CopyRun(preview, files.Count, progress, time);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var cancelled = false;

        try
        {
            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                run.CurrentFile = file.RelativePath;
                var (status, message) = CopyOne(file, preview, options.DryRun, buffer, run);
                run.Complete(new CopyEntry(time.GetLocalNow(), status, file.RelativePath, file.SizeBytes, message));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        run.CurrentFile = null;
        run.Report();

        return new CopyResult
        {
            Started = started,
            Finished = time.GetLocalNow(),
            ScanResultFilePath = options.ScanResultFilePath,
            SourceRoot = preview.SourceRoot,
            DestinationRoot = preview.DestinationRoot,
            DryRun = options.DryRun,
            Cancelled = cancelled,
            Summary = run.ToSummary(),
            Entries = run.Entries,
        };
    }

    internal static CopyPreview Preview(ScanResult scanResult, Func<string, long?> freeSpaceProvider)
    {
        ArgumentNullException.ThrowIfNull(scanResult);

        string source, destination;
        try
        {
            (source, destination) = ScanValidation.ValidateRoots(scanResult.SourceRoot, scanResult.DestinationRoot);
        }
        catch (ScanValidationException ex)
        {
            throw new CopyValidationException(ex.Message, ex);
        }

        return new CopyPreview(
            source,
            destination,
            scanResult.MissingFiles.Count,
            scanResult.MissingFiles.Sum(f => f.SizeBytes),
            freeSpaceProvider(destination));
    }

    private static (CopyStatus Status, string? Message) CopyOne(
        MissingFile file, CopyPreview preview, bool dryRun, byte[] buffer, CopyRun run)
    {
        // The scan result file can be edited by hand: never read or write outside the roots.
        if (!TryResolve(preview.SourceRoot, file.RelativePath, out var sourcePath)
            || !TryResolve(preview.DestinationRoot, file.RelativePath, out var destinationPath))
        {
            return (CopyStatus.Failed, "Invalid relative path: it must stay inside the source and destination folders.");
        }

        string? tempPath = null;
        try
        {
            if (Path.Exists(destinationPath))
            {
                return (CopyStatus.SkippedExists, null);
            }

            var sourceInfo = new FileInfo(sourcePath);
            if (!sourceInfo.Exists)
            {
                return (CopyStatus.SkippedSourceChanged, "The source file no longer exists.");
            }

            if (sourceInfo.Length != file.SizeBytes || sourceInfo.LastWriteTimeUtc != file.LastWriteTimeUtc)
            {
                return (CopyStatus.SkippedSourceChanged, "The source file has changed since the scan (size or last write time differs).");
            }

            if (dryRun)
            {
                return (CopyStatus.WouldCopy, null);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            tempPath = $"{destinationPath}.{Guid.NewGuid():N}{TempSuffix}";

            var copied = CopyContents(sourcePath, tempPath, file.SizeBytes, buffer, run);
            if (copied != file.SizeBytes)
            {
                return (CopyStatus.SkippedSourceChanged, "The source file changed while it was being copied.");
            }

            File.SetLastWriteTimeUtc(tempPath, file.LastWriteTimeUtc);

            try
            {
                File.Move(tempPath, destinationPath, overwrite: false);
            }
            catch (IOException) when (Path.Exists(destinationPath))
            {
                // Created by someone else while we were copying.
                return (CopyStatus.SkippedExists, "The destination file was created by another program during the copy.");
            }

            tempPath = null;
            return (CopyStatus.Copied, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            return (CopyStatus.Failed, ex.Message);
        }
        finally
        {
            if (tempPath is not null)
            {
                TryDelete(tempPath);
            }
        }
    }

    private static long CopyContents(string sourcePath, string tempPath, long expectedLength, byte[] buffer, CopyRun run)
    {
        using var input = new FileStream(sourcePath, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = 0,
            Options = FileOptions.SequentialScan,
        });
        using var output = new FileStream(tempPath, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 0,
            PreallocationSize = expectedLength,
        });

        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, BufferSize)) > 0)
        {
            output.Write(buffer, 0, read);
            total += read;
            run.AddBytes(read);
        }

        return total;
    }

    /// <summary>Combines root and relative path; false if the result would be outside the root.</summary>
    internal static bool TryResolve(string root, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(Path.Join(root, relativePath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return fullPath.StartsWith(ScanValidation.WithTrailingSeparator(root), StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the temporary name makes a leftover easy to recognise.
        }
    }

    internal static string FormatBytes(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes} bytes")
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value:0.0} {units[unit]}");
    }

    /// <summary>Counters and progress reporting of one copy run.</summary>
    private sealed class CopyRun(CopyPreview preview, int totalFiles, IProgress<CopyProgress>? progress, TimeProvider time)
    {
        private readonly long _startTimestamp = time.GetTimestamp();
        private TimeSpan _lastReport;
        private int _copied;
        private int _skippedExists;
        private int _skippedSourceChanged;
        private int _failed;
        private long _bytesCopied;

        // Recorded sizes of completed files (copied or not) and bytes of the current file,
        // used for the time estimate.
        private long _bytesProcessed;
        private long _bytesOfCurrentFile;

        public List<CopyEntry> Entries { get; } = [];

        public string? CurrentFile { get; set; }

        public void AddBytes(int count)
        {
            _bytesCopied += count;
            _bytesOfCurrentFile += count;
            MaybeReport();
        }

        public void Complete(CopyEntry entry)
        {
            Entries.Add(entry);
            _bytesProcessed += entry.SizeBytes;
            _bytesOfCurrentFile = 0;

            switch (entry.Status)
            {
                case CopyStatus.Copied or CopyStatus.WouldCopy:
                    _copied++;
                    break;
                case CopyStatus.SkippedExists:
                    _skippedExists++;
                    break;
                case CopyStatus.SkippedSourceChanged:
                    _skippedSourceChanged++;
                    break;
                default:
                    _failed++;
                    break;
            }

            MaybeReport();
        }

        public CopySummary ToSummary() => new(
            Total: totalFiles,
            Copied: _copied,
            SkippedExists: _skippedExists,
            SkippedSourceChanged: _skippedSourceChanged,
            Failed: _failed,
            NotProcessed: totalFiles - Entries.Count,
            BytesCopied: _bytesCopied);

        public void Report()
        {
            if (progress is null)
            {
                return;
            }

            var elapsed = time.GetElapsedTime(_startTimestamp);
            _lastReport = elapsed;
            progress.Report(new CopyProgress(
                Entries.Count, totalFiles, _copied, _skippedExists + _skippedSourceChanged, _failed,
                _bytesCopied, preview.TotalBytes, elapsed, EstimateRemaining(elapsed), CurrentFile));
        }

        private void MaybeReport()
        {
            if (progress is not null && time.GetElapsedTime(_startTimestamp) - _lastReport >= ProgressInterval)
            {
                Report();
            }
        }

        private TimeSpan? EstimateRemaining(TimeSpan elapsed)
        {
            if (_bytesCopied == 0 || elapsed < TimeSpan.FromSeconds(1))
            {
                return null;
            }

            var bytesPerSecond = _bytesCopied / elapsed.TotalSeconds;
            var remaining = Math.Max(0, preview.TotalBytes - _bytesProcessed - _bytesOfCurrentFile);
            return TimeSpan.FromSeconds(remaining / bytesPerSecond);
        }
    }
}
