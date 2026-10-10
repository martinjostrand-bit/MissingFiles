using MissingFiles.Core.FileTypes;

namespace MissingFiles.FunctionalTests;

/// <summary>The test data generator itself (FT-01).</summary>
public class TestDataTests
{
    [Fact]
    public void NamesAreRealisticAndDistinct()
    {
        var names = TestData.Names(100_000);

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("IMG_0010.JPG", names);
        Assert.Contains("DSC04501.ARW", names);
        Assert.Matches(@"^VID_\d{8}_\d{6}\.mp4$", names[1]);
        Assert.Equal(names.Take(10), TestData.Names(10)); // repeatable
    }

    [Fact]
    public void AllNamesArePicturesOrVideosOfTheDefaultFileTypes()
    {
        var filter = FileTypesFile.LoadDefault().CreateFilter();

        Assert.All(TestData.Names(50), name => Assert.True(filter.Matches(name), name));
    }

    [Fact]
    public void RelativePathsUseCameraAndArchiveFolders()
    {
        var paths = TestData.RelativePaths(14);

        Assert.Contains(paths, p => p.StartsWith(@"DCIM\100CANON\", StringComparison.Ordinal));
        Assert.Contains(paths, p => p.StartsWith(@"2022\Bjørn's birthday\", StringComparison.Ordinal));
        Assert.Equal(TestData.Names(14), paths.Select(Path.GetFileName));
    }

    [Fact]
    public void FilesAreTinyAndContentDependsOnNameAndSize()
    {
        Assert.All(TestData.Names(1000), name => Assert.InRange(TestData.SizeOf(name), 1, TestData.MaxFileSize));

        Assert.Equal(TestData.Content(@"a\IMG_0001.JPG", 300), TestData.Content(@"b\img_0001.jpg", 300));
        Assert.NotEqual(TestData.Content("IMG_0001.JPG", 300), TestData.Content("IMG_0002.JPG", 300));
        Assert.Empty(TestData.Content("IMG_0001.JPG", 0));
    }

    [Fact]
    public void TestFolderIsDeletedAfterTheTest()
    {
        string root;
        using (var folder = new TestFolder())
        {
            root = folder.Root;
            folder.AddSourceFiles(TestData.RelativePaths(5));
            Assert.Equal(5, Directory.GetFiles(folder.Source, "*", SearchOption.AllDirectories).Length);
        }

        Assert.False(Directory.Exists(root));
    }
}
