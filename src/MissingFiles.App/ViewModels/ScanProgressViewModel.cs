using MissingFiles.Core.Scanning;

namespace MissingFiles.App.ViewModels;

/// <summary>Progress dialog content for a scan (REQ-13).</summary>
public sealed class ScanProgressViewModel : ProgressViewModel
{
    private readonly ProgressItem _destination;
    private readonly ProgressItem _source;
    private readonly ProgressItem _matched;
    private readonly ProgressItem _missing;
    private readonly ProgressItem _errors;

    public ScanProgressViewModel(TimeProvider time)
        : base("Scanning", time)
    {
        _destination = AddItem("Destination files read");
        _source = AddItem("Source files scanned");
        _matched = AddItem("Matched");
        _missing = AddItem("Missing");
        _errors = AddItem("Folders not readable");
        Phase = "Reading the destination folder...";
    }

    public void Update(ScanProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        if (IsCancelling)
        {
            return;
        }

        Phase = progress.Phase switch
        {
            ScanPhase.IndexingDestination => "Reading the destination folder...",
            ScanPhase.ScanningSource => "Scanning the source folder...",
            _ => "Finishing...",
        };
        _destination.Value = FormatCount(progress.DestinationFilesIndexed);
        _source.Value = FormatCount(progress.SourceFilesScanned);
        _matched.Value = FormatCount(progress.Matched);
        _missing.Value = FormatCount(progress.Missing);
        _errors.Value = FormatCount(progress.Errors);
    }
}
