using MissingFiles.App.ViewModels;
using MissingFiles.Core.Copying;

namespace MissingFiles.App.Tests;

public class CopyProgressViewModelTests
{
    [Fact]
    public void ShowsFilesBytesSkippedFailedAndRemainingTime() // REQ-30
    {
        using var vm = new CopyProgressViewModel(TimeProvider.System);

        vm.Update(new CopyProgress(3, 10, 2, 1, 0, 3 * 1024 * 1024, 12 * 1024 * 1024, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(75), @"2021\IMG_0004.JPG"));

        Assert.Equal(["Files", "Data", "Skipped", "Failed", "Time remaining"], vm.Items.Select(i => i.Label));
        Assert.Equal(["3 of 10", "3.0 MB of 12.0 MB", "1", "0", "00:01:15"], vm.Items.Select(i => i.Value));
        Assert.Equal(@"Copying 2021\IMG_0004.JPG", vm.Phase);
        Assert.Equal(25, vm.Percent);
        Assert.False(vm.IsIndeterminate);
    }

    [Fact]
    public void RemainingTimeIsCalculatingUntilKnown()
    {
        using var vm = new CopyProgressViewModel(TimeProvider.System);
        Assert.Equal("calculating...", vm.Items[^1].Value);

        vm.Update(new CopyProgress(0, 10, 0, 0, 0, 0, 100, TimeSpan.Zero, null, "a.jpg"));

        Assert.Equal("calculating...", vm.Items[^1].Value);
    }

    [Fact]
    public void PercentUsesFilesWhenAllFilesAreEmpty()
    {
        using var vm = new CopyProgressViewModel(TimeProvider.System);

        vm.Update(new CopyProgress(1, 4, 1, 0, 0, 0, 0, TimeSpan.Zero, null, "a.jpg"));

        Assert.Equal(25, vm.Percent);
    }

    [Fact]
    public void CancelSaysItStopsAfterTheCurrentFile() // REQ-30
    {
        using var vm = new CopyProgressViewModel(TimeProvider.System);

        vm.CancelCommand.Execute(null);
        vm.Update(new CopyProgress(1, 4, 1, 0, 0, 10, 40, TimeSpan.Zero, null, "b.jpg"));

        Assert.True(vm.CancellationToken.IsCancellationRequested);
        Assert.Equal("Cancelling after the current file...", vm.Phase);
        Assert.Equal("1 of 4", vm.Items[0].Value);
    }
}
