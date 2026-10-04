using MissingFiles.Core.Copying;

namespace MissingFiles.App.ViewModels;

/// <summary>Progress dialog content for a copy (REQ-30).</summary>
public sealed class CopyProgressViewModel : ProgressViewModel
{
    private readonly ProgressItem _files;
    private readonly ProgressItem _bytes;
    private readonly ProgressItem _skipped;
    private readonly ProgressItem _failed;
    private readonly ProgressItem _remaining;

    public CopyProgressViewModel(TimeProvider time)
        : base("Copying", time)
    {
        _files = AddItem("Files");
        _bytes = AddItem("Data");
        _skipped = AddItem("Skipped");
        _failed = AddItem("Failed");
        _remaining = AddItem("Time remaining");
        _remaining.Value = "calculating...";
        Phase = "Copying files...";
        Percent = 0;
    }

    protected override string CancellingText => "Cancelling after the current file...";

    public void Update(CopyProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        _files.Value = $"{FormatCount(progress.FilesProcessed)} of {FormatCount(progress.TotalFiles)}";
        _bytes.Value = $"{Copier.FormatBytes(progress.BytesCopied)} of {Copier.FormatBytes(progress.TotalBytes)}";
        _skipped.Value = FormatCount(progress.Skipped);
        _failed.Value = FormatCount(progress.Failed);
        _remaining.Value = progress.EstimatedRemaining is { } eta ? FormatDuration(eta) : "calculating...";

        // By bytes, as large videos take much longer than small pictures; by files if all are empty.
        Percent = progress.TotalBytes > 0 ? 100.0 * progress.BytesCopied / progress.TotalBytes
            : progress.TotalFiles > 0 ? 100.0 * progress.FilesProcessed / progress.TotalFiles
            : 100;

        if (!IsCancelling)
        {
            Phase = progress.CurrentFile is { } file ? $"Copying {file}" : "Finishing...";
        }
    }
}
