using System.Globalization;

using MissingFiles.App.ViewModels;
using MissingFiles.Core.Scanning;

namespace MissingFiles.App.Tests;

public class ScanProgressViewModelTests
{
    [Fact]
    public void ShowsPhaseAndCounters() // REQ-13
    {
        using var vm = new ScanProgressViewModel(TimeProvider.System);

        vm.Update(new ScanProgress(ScanPhase.ScanningSource, 1200, 3456, 3000, 456, 2, TimeSpan.FromSeconds(5)));

        Assert.Equal("Scanning the source folder...", vm.Phase);
        Assert.Equal(
            ["Destination files read", "Source files scanned", "Matched", "Missing", "Folders not readable"],
            vm.Items.Select(i => i.Label));
        Assert.Equal(
            [1200.ToString("N0", CultureInfo.CurrentCulture), 3456.ToString("N0", CultureInfo.CurrentCulture), 3000.ToString("N0", CultureInfo.CurrentCulture), "456", "2"],
            vm.Items.Select(i => i.Value));
        Assert.True(vm.IsIndeterminate);
    }

    [Fact]
    public void CancelSignalsTokenOnceAndKeepsCancellingText() // REQ-15
    {
        using var vm = new ScanProgressViewModel(TimeProvider.System);

        vm.CancelCommand.Execute(null);
        vm.Update(new ScanProgress(ScanPhase.ScanningSource, 1, 1, 1, 0, 0, TimeSpan.Zero));

        Assert.True(vm.CancellationToken.IsCancellationRequested);
        Assert.True(vm.IsCancelling);
        Assert.Equal("Cancelling...", vm.Phase);
        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void ElapsedTimeComesFromTheClock() // REQ-14
    {
        var clock = new ManualClock();
        using var vm = new ScanProgressViewModel(clock);

        clock.Advance(TimeSpan.FromSeconds(75));
        vm.RefreshElapsed();

        Assert.Equal("00:01:15", vm.ElapsedText);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan by) => _timestamp += by.Ticks;
    }
}
