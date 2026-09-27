using System.Runtime.InteropServices;

namespace MissingFiles.Core.Scanning;

/// <summary>
/// Compares a source folder with a destination folder (spec §4.3, §4.4).
/// </summary>
public static class Scanner
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Runs a scan. The method is synchronous; UI callers should run it on a background thread.
    /// </summary>
    /// <param name="options">Folders and file types.</param>
    /// <param name="progress">Receives progress at least every 200 ms, and once when completed.</param>
    /// <param name="cancellationToken">Cancels the scan (REQ-15).</param>
    /// <exception cref="ScanValidationException">The folders cannot be used (REQ-02).</exception>
    /// <exception cref="OperationCanceledException">The scan was cancelled.</exception>
    public static ScanResult Run(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var (sourceRoot, destinationRoot) = ScanValidation.ValidateRoots(options.SourceRoot, options.DestinationRoot);
        var time = options.TimeProvider;
        var started = time.GetLocalNow();
        var errors = new List<ScanError>();
        var tracker = new ProgressTracker(progress, time, errors);

        // Phase 1: index the destination by (name, size), and by name alone for REQ-10.
        var destinationFiles = new HashSet<FileKey>(FileKeyComparer.Instance);
        var destinationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in FileWalker.EnumerateFiles(destinationRoot, options.Filter, errors.Add, cancellationToken))
        {
            destinationFiles.Add(new FileKey(file.Name, file.Length));
            destinationNames.Add(file.Name);
            tracker.DestinationFilesIndexed++;
            tracker.OnFile(cancellationToken);
        }

        // Phase 2: match each source file.
        tracker.Phase = ScanPhase.ScanningSource;
        var sourceNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<MissingFile>();
        foreach (var file in FileWalker.EnumerateFiles(sourceRoot, options.Filter, errors.Add, cancellationToken))
        {
            CollectionsMarshal.GetValueRefOrAddDefault(sourceNameCounts, file.Name, out _)++;

            if (destinationFiles.Contains(new FileKey(file.Name, file.Length)))
            {
                tracker.Matched++;
            }
            else
            {
                missing.Add(new MissingFile(
                    file.RelativePath,
                    file.Length,
                    file.LastWriteTimeUtc,
                    SameNameDifferentSize: destinationNames.Contains(file.Name)));
                tracker.Missing++;
            }

            tracker.SourceFilesScanned++;
            tracker.OnFile(cancellationToken);
        }

        missing.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.RelativePath, b.RelativePath));
        tracker.Phase = ScanPhase.Completed;
        tracker.Report();

        return new ScanResult
        {
            ScanStarted = started,
            ScanFinished = time.GetLocalNow(),
            SourceRoot = sourceRoot,
            DestinationRoot = destinationRoot,
            FileTypesFilePath = options.FileTypesFilePath,
            Extensions = options.Filter.Extensions,
            Summary = new ScanSummary(
                SourceFilesScanned: tracker.SourceFilesScanned,
                Matched: tracker.Matched,
                Missing: tracker.Missing,
                Errors: errors.Count,
                DuplicateNameGroups: sourceNameCounts.Values.Count(count => count > 1),
                SameNameDifferentSize: missing.Count(m => m.SameNameDifferentSize)),
            MissingFiles = missing,
            Errors = errors,
        };
    }

    private readonly record struct FileKey(string Name, long Length);

    /// <summary>Same name (case-insensitive, as Windows) and same size (REQ-08).</summary>
    private sealed class FileKeyComparer : IEqualityComparer<FileKey>
    {
        public static readonly FileKeyComparer Instance = new();

        public bool Equals(FileKey x, FileKey y) =>
            x.Length == y.Length && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(FileKey obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name), obj.Length);
    }

    private sealed class ProgressTracker(IProgress<ScanProgress>? progress, TimeProvider time, List<ScanError> errors)
    {
        private readonly long _startTimestamp = time.GetTimestamp();
        private TimeSpan _lastReport;
        private int _filesSinceCheck;

        public ScanPhase Phase { get; set; } = ScanPhase.IndexingDestination;

        public int DestinationFilesIndexed { get; set; }

        public int SourceFilesScanned { get; set; }

        public int Matched { get; set; }

        public int Missing { get; set; }

        /// <summary>Checks for cancellation and reports progress, both at a limited rate.</summary>
        public void OnFile(CancellationToken cancellationToken)
        {
            if (++_filesSinceCheck < 256)
            {
                return;
            }

            _filesSinceCheck = 0;
            cancellationToken.ThrowIfCancellationRequested();

            if (progress is not null && time.GetElapsedTime(_startTimestamp) - _lastReport >= ProgressInterval)
            {
                Report();
            }
        }

        public void Report()
        {
            if (progress is null)
            {
                return;
            }

            _lastReport = time.GetElapsedTime(_startTimestamp);
            progress.Report(new ScanProgress(
                Phase, DestinationFilesIndexed, SourceFilesScanned, Matched, Missing, errors.Count, _lastReport));
        }
    }
}
