using System.ComponentModel;

using MissingFiles.App.ViewModels;
using MissingFiles.Core.Scanning;

namespace MissingFiles.App.Tests;

public class ScanResultViewModelTests
{
    private static readonly DateTimeOffset Started = new(2026, 9, 27, 14, 3, 12, TimeSpan.FromHours(2));

    [Fact]
    public void RowsShowPathSizeDateAndSameNameFlag() // REQ-21
    {
        var vm = Create(new MissingFile(@"2021\IMG.JPG", 4839211, new DateTime(2021, 7, 14, 10, 22, 5, DateTimeKind.Utc), true));

        var row = Assert.Single(vm.VisibleMissingFiles);

        Assert.Equal(@"2021\IMG.JPG", row.RelativePath);
        Assert.Equal("4.6 MB", row.SizeText);
        Assert.Equal(new DateTime(2021, 7, 14, 10, 22, 5, DateTimeKind.Utc).ToLocalTime(), row.LastWriteTime);
        Assert.Equal("Yes", row.SameNameDifferentSizeText);
    }

    [Fact]
    public void RowsAreSortedByPathInitially()
    {
        var vm = Create(File("b.jpg"), File("A.jpg"), File("c.jpg"));

        Assert.Equal(["A.jpg", "b.jpg", "c.jpg"], Paths(vm));
    }

    [Fact]
    public void FilterShowsMatchingPathsCaseInsensitively() // REQ-21
    {
        var vm = Create(File(@"2021\Summer\IMG_1.JPG"), File(@"2022\IMG_2.JPG"), File(@"2021\VID_1.MP4"));

        vm.FilterText = "2021";
        Assert.Equal([@"2021\Summer\IMG_1.JPG", @"2021\VID_1.MP4"], Paths(vm));
        Assert.Equal("Showing 2 of 3 files", vm.VisibleCountText);

        vm.FilterText = "summer";
        Assert.Equal([@"2021\Summer\IMG_1.JPG"], Paths(vm));

        vm.FilterText = "  ";
        Assert.Equal(3, vm.VisibleMissingFiles.Count);
        Assert.Equal("3 files", vm.VisibleCountText);
    }

    [Fact]
    public void SortBySizeDescendingThenPath() // REQ-21
    {
        var vm = Create(File("a.jpg", 10), File("b.jpg", 30), File("c.jpg", 20), File("d.jpg", 30));

        vm.Sort(ScanResultViewModel.SortBySize, ListSortDirection.Descending);

        Assert.Equal(["d.jpg", "b.jpg", "c.jpg", "a.jpg"], Paths(vm));
    }

    [Fact]
    public void SortingIsKeptWhenFilterChanges()
    {
        var vm = Create(File("a1.jpg", 10), File("a2.jpg", 30), File("b.jpg", 20));
        vm.Sort(ScanResultViewModel.SortBySize, ListSortDirection.Ascending);

        vm.FilterText = "a";

        Assert.Equal(["a1.jpg", "a2.jpg"], Paths(vm));
    }

    [Theory]
    [InlineData(ScanResultViewModel.SortByDate)]
    [InlineData(ScanResultViewModel.SortBySameName)]
    [InlineData(ScanResultViewModel.SortByPath)]
    public void AllColumnsCanBeSorted(string member)
    {
        var vm = Create(
            new MissingFile("x.jpg", 1, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), true),
            new MissingFile("y.jpg", 1, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc), false));

        vm.Sort(member, ListSortDirection.Descending);

        Assert.Equal(member == ScanResultViewModel.SortByDate ? "y.jpg" : member == ScanResultViewModel.SortBySameName ? "x.jpg" : "y.jpg", vm.VisibleMissingFiles[0].RelativePath);
    }

    [Fact]
    public void ShowResultFileOpensExplorer() // REQ-20
    {
        var shell = new FakeShellService();
        var vm = new ScanResultViewModel(Result([]), @"C:\out\MissingFiles_1.json", shell);

        vm.OpenResultFolderCommand.Execute(null);

        Assert.Equal(@"C:\out\MissingFiles_1.json", Assert.Single(shell.ShownInExplorer));
    }

    [Fact]
    public void SummaryAndWarningsAreExposed() // REQ-20
    {
        var result = Result([File("a.jpg")]) is var r
            ? new ScanResult
            {
                ScanStarted = r.ScanStarted,
                ScanFinished = r.ScanFinished,
                SourceRoot = r.SourceRoot,
                DestinationRoot = r.DestinationRoot,
                Extensions = r.Extensions,
                Summary = new ScanSummary(5, 4, 1, 1, 2, 0),
                MissingFiles = r.MissingFiles,
                Errors = [new ScanError(@"D:\locked", "Access denied")],
            }
            : null!;

        var vm = new ScanResultViewModel(result, "r.json", new FakeShellService());

        Assert.True(vm.HasErrors);
        Assert.True(vm.HasDuplicateNames);
        Assert.Contains("took 00:01:28", vm.ScanTimeText, StringComparison.Ordinal);
    }

    [Fact]
    public void LargeListFiltersAndSortsQuickly() // NFR-03a
    {
        var files = Enumerable.Range(0, 200_000).Select(i => File($@"folder{i % 100}\IMG_{i:D6}.JPG", i)).ToArray();
        var vm = Create(files);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        vm.FilterText = "folder7";
        vm.Sort(ScanResultViewModel.SortBySize, ListSortDirection.Descending);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"Took {watch.Elapsed}");
        Assert.Equal(22_000, vm.VisibleMissingFiles.Count); // folder7 and folder70..79
    }

    private static MissingFile File(string path, long size = 10) =>
        new(path, size, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc), false);

    private static ScanResultViewModel Create(params MissingFile[] files) =>
        new(Result(files), "result.json", new FakeShellService());

    private static ScanResult Result(MissingFile[] files) => new()
    {
        ScanStarted = Started,
        ScanFinished = Started.AddSeconds(88),
        SourceRoot = @"D:\src",
        DestinationRoot = @"E:\dst",
        Extensions = [".jpg"],
        Summary = new ScanSummary(files.Length, 0, files.Length, 0, 0, 0),
        MissingFiles = files,
        Errors = [],
    };

    private static string[] Paths(ScanResultViewModel vm) => [.. vm.VisibleMissingFiles.Select(r => r.RelativePath)];
}
