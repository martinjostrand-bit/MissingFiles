using MissingFiles.Core.FileTypes;

namespace MissingFiles.Core.Tests.FileTypes;

public class ExtensionFilterTests
{
    [Theory]
    [InlineData("jpg", ".jpg")]
    [InlineData("JPG", ".jpg")]
    [InlineData(".Jpg", ".jpg")]
    [InlineData("  .png ", ".png")]
    [InlineData("*", "*")]
    [InlineData("", "")]
    public void NormalizeAcceptsCommonSpellings(string input, string expected)
    {
        Assert.Equal(expected, ExtensionFilter.Normalize(input));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("*.jpg")]
    [InlineData(".tar.gz")]
    [InlineData(".j?g")]
    [InlineData("a/b")]
    [InlineData(".my ext")]
    [InlineData(null)]
    public void NormalizeRejectsInvalidExtensions(string? input)
    {
        Assert.Throws<ArgumentException>(() => ExtensionFilter.Normalize(input));
    }

    [Fact]
    public void MatchesIsCaseInsensitive() // TC-21
    {
        var filter = new ExtensionFilter(["JPG"]);

        Assert.True(filter.Matches("IMG_0001.jpg"));
        Assert.True(filter.Matches("IMG_0001.JPG"));
        Assert.True(filter.Matches("img.Jpg"));
        Assert.False(filter.Matches("IMG_0001.jpeg"));
        Assert.False(filter.Matches("IMG_0001"));
    }

    [Fact]
    public void AllFilesMatchesEverythingIncludingNoExtension() // TC-19
    {
        var filter = new ExtensionFilter(["*"]);

        Assert.True(filter.IncludesAllFiles);
        Assert.True(filter.Matches("notes.txt"));
        Assert.True(filter.Matches("README"));
    }

    [Fact]
    public void EmptyExtensionMatchesOnlyFilesWithoutExtension() // TC-20
    {
        var filter = new ExtensionFilter([""]);

        Assert.True(filter.Matches("README"));
        Assert.True(filter.Matches("trailingdot."));
        Assert.False(filter.Matches("notes.txt"));
    }

    [Fact]
    public void ExtensionsAreNormalizedDeduplicatedAndSorted()
    {
        var filter = new ExtensionFilter(["mp4", ".JPG", "jpg", ".Jpg"]);

        Assert.Equal([".jpg", ".mp4"], filter.Extensions);
    }

    [Fact]
    public void EmptyListIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ExtensionFilter([]));
    }
}
