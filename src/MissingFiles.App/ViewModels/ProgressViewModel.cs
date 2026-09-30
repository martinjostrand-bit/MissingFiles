using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissingFiles.App.ViewModels;

/// <summary>A label and value shown in the progress dialog.</summary>
public sealed partial class ProgressItem(string label) : ObservableObject
{
    public string Label { get; } = label;

    [ObservableProperty]
    public partial string Value { get; set; } = "0";
}

/// <summary>
/// Content of the modal progress dialog (REQ-13, REQ-30): a phase, counters,
/// elapsed time and a Cancel button. Scan and copy derive from it.
/// </summary>
public abstract partial class ProgressViewModel : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly TimeProvider _time;
    private readonly long _startTimestamp;

    protected ProgressViewModel(string title, TimeProvider time)
    {
        Title = title;
        _time = time;
        _startTimestamp = time.GetTimestamp();
    }

    public string Title { get; }

    [ObservableProperty]
    public partial string Phase { get; set; } = "Starting...";

    public ObservableCollection<ProgressItem> Items { get; } = [];

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = "00:00:00";

    /// <summary>Progress in percent, or null when unknown (the bar then runs without end).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    public partial double? Percent { get; set; }

    public bool IsIndeterminate => Percent is null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsCancelling { get; set; }

    public CancellationToken CancellationToken => _cancellation.Token;

    /// <summary>
    /// Updates the elapsed time. Called by a timer in the dialog, so the time keeps
    /// running even while no progress is reported (REQ-14).
    /// </summary>
    public void RefreshElapsed() => ElapsedText = FormatDuration(_time.GetElapsedTime(_startTimestamp));

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel()
    {
        IsCancelling = true;
        Phase = "Cancelling...";
        _cancellation.Cancel();
    }

    private bool CanCancel() => !IsCancelling;

    public void Dispose()
    {
        _cancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    protected ProgressItem AddItem(string label)
    {
        var item = new ProgressItem(label);
        Items.Add(item);
        return item;
    }

    protected static string FormatCount(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    internal static string FormatDuration(TimeSpan value) => value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
}
