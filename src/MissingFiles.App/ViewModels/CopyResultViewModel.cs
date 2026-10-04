using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MissingFiles.App.Services;
using MissingFiles.Core.Copying;

namespace MissingFiles.App.ViewModels;

/// <summary>A file that was not copied, with the reason (REQ-26, REQ-27, REQ-29).</summary>
public sealed class CopyProblemRow(CopyEntry entry)
{
    public string Status { get; } = CopyLogFile.StatusText(entry.Status);

    public string RelativePath { get; } = entry.RelativePath;

    public string Message { get; } = entry.Message ?? string.Empty;
}

/// <summary>The result of a copy run (REQ-31): counters, the copy log and the files that were not copied.</summary>
public sealed partial class CopyResultViewModel : ObservableObject
{
    private readonly IShellService _shell;

    /// <param name="result">The finished copy.</param>
    /// <param name="logFilePath">Path of the copy log, or null if it could not be written.</param>
    /// <param name="shell">Used to show the log in Explorer.</param>
    public CopyResultViewModel(CopyResult result, string? logFilePath, IShellService shell)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
        LogFilePath = logFilePath;
        _shell = shell;
        Problems = [.. result.Entries
            .Where(e => e.Status is not (CopyStatus.Copied or CopyStatus.WouldCopy))
            .Select(e => new CopyProblemRow(e))];
    }

    public CopyResult Result { get; }

    public CopySummary Summary => Result.Summary;

    public string? LogFilePath { get; }

    public bool HasLogFile => LogFilePath is not null;

    /// <summary>E.g. "Copy finished, took 00:01:05" or "Copy cancelled after 00:00:12".</summary>
    public string HeadlineText => Result.Cancelled
        ? $"Copy cancelled after {ProgressViewModel.FormatDuration(Result.Duration)}"
        : $"Copy finished, took {ProgressViewModel.FormatDuration(Result.Duration)}";

    public string BytesCopiedText => Copier.FormatBytes(Summary.BytesCopied);

    public bool HasNotProcessed => Summary.NotProcessed > 0;

    /// <summary>Files skipped or failed, in processing order.</summary>
    public IReadOnlyList<CopyProblemRow> Problems { get; }

    public bool HasProblems => Problems.Count > 0;

    /// <summary>A one-line summary for the status bar.</summary>
    public string StatusText => string.Create(
        CultureInfo.CurrentCulture,
        $"{(Result.Cancelled ? "Copy cancelled" : "Copy finished")}: {Summary.Copied:N0} copied, "
        + $"{Summary.SkippedExists + Summary.SkippedSourceChanged:N0} skipped, {Summary.Failed:N0} failed.");

    [RelayCommand(CanExecute = nameof(HasLogFile))]
    private void OpenLogFolder() => _shell.ShowInExplorer(LogFilePath!);
}
