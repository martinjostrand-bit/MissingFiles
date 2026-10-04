using MissingFiles.App.ViewModels;
using MissingFiles.Core.Copying;

namespace MissingFiles.App.Tests;

public class CopyResultViewModelTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 4, 10, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void ListsOnlyFilesThatWereNotCopied() // REQ-31
    {
        var vm = Create(
            cancelled: false,
            Entry(CopyStatus.Copied, "a.jpg"),
            Entry(CopyStatus.SkippedExists, "b.jpg"),
            Entry(CopyStatus.SkippedSourceChanged, "c.jpg", "The source file no longer exists."),
            Entry(CopyStatus.Failed, "d.jpg", "Access denied"));

        Assert.True(vm.HasProblems);
        Assert.Equal(["b.jpg", "c.jpg", "d.jpg"], vm.Problems.Select(p => p.RelativePath));
        Assert.Equal(["Skipped - exists", "Skipped - source changed", "Failed"], vm.Problems.Select(p => p.Status));
        Assert.Equal(["", "The source file no longer exists.", "Access denied"], vm.Problems.Select(p => p.Message));
        Assert.Equal("Copy finished, took 00:01:05", vm.HeadlineText);
        Assert.Equal("Copy finished: 1 copied, 2 skipped, 1 failed.", vm.StatusText);
        Assert.False(vm.HasNotProcessed);
    }

    [Fact]
    public void CancelledCopyShowsNotProcessed()
    {
        var vm = Create(cancelled: true, Entry(CopyStatus.Copied, "a.jpg"));

        Assert.Equal("Copy cancelled after 00:01:05", vm.HeadlineText);
        Assert.True(vm.HasNotProcessed);
        Assert.Equal(3, vm.Summary.NotProcessed);
        Assert.False(vm.HasProblems);
        Assert.StartsWith("Copy cancelled:", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowLogInFolder() // REQ-31
    {
        var shell = new FakeShellService();
        var vm = new CopyResultViewModel(Result(false, []), @"C:\out\MissingFilesCopy_1.log", shell);

        Assert.True(vm.HasLogFile);
        vm.OpenLogFolderCommand.Execute(null);

        Assert.Equal(@"C:\out\MissingFilesCopy_1.log", Assert.Single(shell.ShownInExplorer));
    }

    private static CopyResultViewModel Create(bool cancelled, params CopyEntry[] entries) =>
        new(Result(cancelled, entries), "copy.log", new FakeShellService());

    private static CopyEntry Entry(CopyStatus status, string path, string? message = null) =>
        new(Started, status, path, 10, message);

    private static CopyResult Result(bool cancelled, CopyEntry[] entries) => new()
    {
        Started = Started,
        Finished = Started.AddSeconds(65),
        SourceRoot = @"D:\src",
        DestinationRoot = @"E:\dst",
        DryRun = false,
        Cancelled = cancelled,
        Summary = new CopySummary(
            Total: 4,
            Copied: entries.Count(e => e.Status == CopyStatus.Copied),
            SkippedExists: entries.Count(e => e.Status == CopyStatus.SkippedExists),
            SkippedSourceChanged: entries.Count(e => e.Status == CopyStatus.SkippedSourceChanged),
            Failed: entries.Count(e => e.Status == CopyStatus.Failed),
            NotProcessed: 4 - entries.Length,
            BytesCopied: 10),
        Entries = entries,
    };
}
