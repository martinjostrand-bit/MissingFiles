using MissingFiles.Core.FileTypes;

namespace MissingFiles.Core.Tests.FileTypes;

public class FileTypesFileTests
{
    private const string TwoGroups = """
        {
          "schemaVersion": 1,
          "groups": [
            { "name": "Pictures", "enabled": true, "extensions": ["jpg", ".PNG"] },
            { "name": "Documents", "enabled": false, "extensions": [".pdf"] }
          ]
        }
        """;

    [Fact]
    public void DefaultHasPicturesAndVideosEnabledAndDocumentsDisabled()
    {
        var definition = FileTypesFile.LoadDefault();

        Assert.Equal(["Pictures", "Videos", "Documents"], definition.Groups.Select(g => g.Name));
        Assert.True(definition.Groups[0].Enabled);
        Assert.True(definition.Groups[1].Enabled);
        Assert.False(definition.Groups[2].Enabled);

        var filter = definition.CreateFilter();
        Assert.True(filter.Matches("IMG_0001.JPG"));
        Assert.True(filter.Matches("DSC04512.ARW"));
        Assert.True(filter.Matches("VID_20210714_102205.mp4"));
        Assert.False(filter.Matches("report.pdf"));
    }

    [Fact]
    public void DefaultFileNextToExecutablesEqualsBuiltInDefault()
    {
        var shipped = Path.Combine(AppContext.BaseDirectory, FileTypesFile.DefaultFileName);

        var fromFile = FileTypesFile.Load(shipped);

        Assert.Equal(
            FileTypesFile.LoadDefault().CreateFilter().Extensions,
            fromFile.CreateFilter().Extensions);
    }

    [Fact]
    public void ExtensionsAreNormalized() // TC-21
    {
        var definition = FileTypesFile.Parse(TwoGroups, "test.json");

        Assert.Equal([".jpg", ".png"], definition.Groups[0].Extensions);
    }

    [Fact]
    public void OnlyEnabledGroupsAreUsed() // TC-18
    {
        var filter = FileTypesFile.Parse(TwoGroups, "test.json").CreateFilter();

        Assert.Equal([".jpg", ".png"], filter.Extensions);
    }

    [Fact]
    public void GroupOverrideEnablesDisabledGroup() // TC-22
    {
        var filter = FileTypesFile.Parse(TwoGroups, "test.json").CreateFilter(["documents"]);

        Assert.Equal([".pdf"], filter.Extensions);
    }

    [Fact]
    public void UnknownGroupInOverrideIsRejected()
    {
        var definition = FileTypesFile.Parse(TwoGroups, "test.json");

        var ex = Assert.Throws<FileTypesException>(() => definition.CreateFilter(["Music"]));

        Assert.Contains("Music", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Available groups: Pictures, Documents", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoEnabledExtensionsIsRejected() // TC-23
    {
        const string json = """{ "schemaVersion": 1, "groups": [ { "name": "A", "enabled": false, "extensions": [".jpg"] } ] }""";
        var definition = FileTypesFile.Parse(json, "test.json");

        var ex = Assert.Throws<FileTypesException>(() => definition.CreateFilter());

        Assert.Contains("No file types are enabled", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtensionInSeveralGroupsIsCountedOnce()
    {
        const string json = """
            { "schemaVersion": 1, "groups": [
              { "name": "A", "extensions": [".jpg", "JPG"] },
              { "name": "B", "extensions": [".jpg", ".mp4"] } ] }
            """;

        var filter = FileTypesFile.Parse(json, "test.json").CreateFilter();

        Assert.Equal([".jpg", ".mp4"], filter.Extensions);
    }

    [Fact]
    public void MissingEnabledMeansEnabled()
    {
        const string json = """{ "schemaVersion": 1, "groups": [ { "name": "A", "extensions": [".jpg"] } ] }""";

        Assert.True(FileTypesFile.Parse(json, "test.json").Groups[0].Enabled);
    }

    [Fact]
    public void CommentsTrailingCommasAndUnknownPropertiesAreAccepted()
    {
        const string json = """
            {
              // user comment
              "schemaVersion": 1,
              "futureSetting": { "x": 1 },
              "groups": [ { "name": "A", "extensions": [".jpg",], "color": "red" }, ],
            }
            """;

        Assert.Single(FileTypesFile.Parse(json, "test.json").Groups);
    }

    [Fact]
    public void InvalidJsonReportsFileAndLineNumber() // TC-23
    {
        const string json = "{\n  \"schemaVersion\": 1,\n  \"groups\": [ oops ]\n}";

        var ex = Assert.Throws<FileTypesException>(() => FileTypesFile.Parse(json, @"C:\x\types.json"));

        Assert.Equal(@"C:\x\types.json", ex.FileName);
        Assert.Contains(@"C:\x\types.json", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line 3", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("null", "does not contain a JSON object")]
    [InlineData("""{ "groups": [] }""", "'schemaVersion' is missing")]
    [InlineData("""{ "schemaVersion": 2, "groups": [] }""", "schemaVersion 2 is not supported")]
    [InlineData("""{ "schemaVersion": 1 }""", "'groups' is missing or empty")]
    [InlineData("""{ "schemaVersion": 1, "groups": [] }""", "'groups' is missing or empty")]
    [InlineData("""{ "schemaVersion": 1, "groups": [ null ] }""", "Group 1 is empty")]
    [InlineData("""{ "schemaVersion": 1, "groups": [ { "extensions": [] } ] }""", "Group 1 has no 'name'")]
    [InlineData("""{ "schemaVersion": 1, "groups": [ { "name": "A" } ] }""", "Group 'A' has no 'extensions' list")]
    [InlineData("""{ "schemaVersion": 1, "groups": [ { "name": "A", "extensions": ["*.jpg"] } ] }""", "Group 'A': '*.jpg' is not a valid extension")]
    [InlineData("""{ "schemaVersion": 1, "groups": [ { "name": "A", "extensions": [] }, { "name": "a", "extensions": [] } ] }""", "Group name 'A' is used more than once")]
    [InlineData("""{ "schemaVersion": 1, "groups": [ { "name": "A", "enabled": "yes", "extensions": [] } ] }""", "Invalid JSON at line 1")]
    public void InvalidContentIsRejectedWithClearMessage(string json, string expectedMessage) // TC-23
    {
        var ex = Assert.Throws<FileTypesException>(() => FileTypesFile.Parse(json, "test.json"));

        Assert.Contains(expectedMessage, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadMissingFileNamesTheFile() // TC-23
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");

        var ex = Assert.Throws<FileTypesException>(() => FileTypesFile.Load(path));

        Assert.Equal(path, ex.FileName);
        Assert.Contains("could not be read", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureUserFileCreatesDefaultAndNeverOverwrites()
    {
        using var folder = new TestFolder();
        var path = Path.Combine(folder.Root, "settings", "FileTypes.json");

        Assert.Equal(path, FileTypesFile.EnsureUserFile(path));
        Assert.Equal(3, FileTypesFile.Load(path).Groups.Count);

        File.WriteAllText(path, TwoGroups);
        FileTypesFile.EnsureUserFile(path);
        Assert.Equal(TwoGroups, File.ReadAllText(path));
    }

    [Fact]
    public void UserFileIsInAppData()
    {
        Assert.EndsWith(Path.Combine("MissingFiles", "FileTypes.json"), FileTypesFile.UserFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            FileTypesFile.UserFilePath,
            StringComparison.OrdinalIgnoreCase);
    }
}
