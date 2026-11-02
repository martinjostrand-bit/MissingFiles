using MissingFiles.App.Services;

namespace MissingFiles.App.Tests;

public class AppDataFolderTests
{
    [Fact]
    public void WithoutAFolderTheFilesAreInAppData() // REQ-04, REQ-05b
    {
        Assert.Equal(Environment.ExpandEnvironmentVariables(@"%APPDATA%\MissingFiles\settings.json"), AppDataFolder.SettingsPath(null));
        Assert.Equal(Environment.ExpandEnvironmentVariables(@"%APPDATA%\MissingFiles\FileTypes.json"), AppDataFolder.UserFileTypesPath(null));
    }

    [Fact]
    public void WithAFolderTheFilesAreInThatFolder() // REQ-04, REQ-04a, REQ-05b
    {
        Assert.Equal(@"C:\Data\settings.json", AppDataFolder.SettingsPath(@"C:\Data"));
        Assert.Equal(@"C:\Data\FileTypes.json", AppDataFolder.UserFileTypesPath(@"C:\Data"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingOrEmptyValueCountsAsNotSet(string? value) // REQ-04a
    {
        Assert.True(AppDataFolder.TryResolve(value, out var folder));
        Assert.Null(folder);
    }

    [Theory]
    [InlineData(@"C:\Data")]
    [InlineData(@"D:\")]
    [InlineData(@"\\server\share")]
    [InlineData(@"\\server\share\data")]
    [InlineData(@"\\?\C:\Data")]
    public void AbsolutePathIsTheFolder(string value) // REQ-04a
    {
        Assert.True(AppDataFolder.TryResolve(value, out var folder));
        Assert.Equal(value, folder);
    }

    [Theory]
    [InlineData("data")]
    [InlineData(@"relative\data")]
    [InlineData(@".\data")]
    [InlineData(@"..\data")]
    [InlineData(@"\data")]
    [InlineData("C:data")]
    [InlineData(" ")]
    [InlineData(@"\\")]
    [InlineData(@"\\server")]
    [InlineData(@"\\server\")]
    [InlineData(@"\\?\")]
    [InlineData(@"\\.\")]
    public void OtherValueIsRejected(string value) // REQ-04a
    {
        Assert.False(AppDataFolder.TryResolve(value, out _));
    }
}
