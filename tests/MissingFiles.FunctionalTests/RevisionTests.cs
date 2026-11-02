using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using MissingFiles.Cli;
using MissingFiles.Core;

namespace MissingFiles.FunctionalTests;

/// <summary>Where the revision comes from, using the build definition of the repository (spec CM-01, CM-02).</summary>
public sealed class RevisionDefinitionTests
{
    private const string BuildNumberVariable = "MissingFilesBuildNumber";

    private static string CoreProject => Path.Join(Repository.Root, "src", "MissingFiles.Core", "MissingFiles.Core.csproj");

    [Fact]
    public void LocalBuildHasBuildNumberZero() // CM-02
    {
        // The other workflows have a run number too, but it is not the one of the CI workflow.
        var environment = new Dictionary<string, string?> { [BuildNumberVariable] = null, ["GITHUB_RUN_NUMBER"] = "999" };

        Assert.Equal(RevisionBuild.WithBuildNumber("0"), Evaluate(environment));
    }

    [Fact]
    public void BuildNumberComesFromTheVariableSetByTheCiWorkflow() // CM-02
    {
        Assert.Equal(RevisionBuild.WithBuildNumber("123"), Evaluate(new Dictionary<string, string?> { [BuildNumberVariable] = "123" }));
    }

    [Fact]
    public void CiWorkflowSetsTheVariableToItsRunNumber() // CM-02
    {
        var workflow = File.ReadAllText(Path.Join(Repository.Root, ".github", "workflows", "ci.yml"));

        Assert.Matches($@"(?m)^env:(\r?\n  .*)*\r?\n  {BuildNumberVariable}: \${{{{ github\.run_number }}}}\r?$", workflow);
    }

    [Fact]
    public void RevisionIsDefinedOnlyInDirectoryBuildProps() // CM-02, CM-05
    {
        var version = new Regex("<(Version|VersionPrefix|VersionSuffix|AssemblyVersion|FileVersion|InformationalVersion)[ >]", RegexOptions.None, TimeSpan.FromSeconds(1));
        var projects = Directory.GetFiles(Repository.Root, "*.csproj", SearchOption.AllDirectories);

        Assert.NotEmpty(projects);
        Assert.All(projects, project => Assert.DoesNotMatch(version, File.ReadAllText(project)));
        Assert.Matches(version, File.ReadAllText(Path.Join(Repository.Root, "Directory.Build.props")));
    }

    [Theory]
    [InlineData("MissingFiles.App")]
    [InlineData("MissingFiles.Cli")]
    public void ManifestHasNoVersionOfItsOwn(string project) // CM-02, CM-05
    {
        var manifest = File.ReadAllText(Path.Join(Repository.Root, "src", project, "app.manifest"));

        Assert.DoesNotContain("<assemblyIdentity", manifest, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("123")]
    [InlineData("65534")]
    public void ValidBuildNumberIsAccepted(string buildNumber) // CM-01
    {
        var result = Dotnet.Run(Repository.Root, ["msbuild", CoreProject, "-t:CheckRevision", $"-p:{BuildNumberVariable}={buildNumber}"]);

        Assert.True(result.ExitCode == 0, result.Output);
    }

    [Theory]
    [InlineData("65535")] // Above the Windows file version limit.
    [InlineData("007")] // Leading zeros.
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("")]
    public void BuildStopsOnInvalidBuildNumber(string buildNumber) // CM-01
    {
        // The check comes before anything is written, so this does not touch the build output of the repository.
        var result = Dotnet.Run(Repository.Root, ["build", CoreProject, "--no-restore", $"-p:{BuildNumberVariable}={buildNumber}"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Revision '{RevisionBuild.WithBuildNumber(buildNumber)}' is not valid", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("65535.0.0.1")]
    [InlineData("1.00.0.1")]
    [InlineData("1.0.0")]
    [InlineData("1.0.0.0.1")]
    public void BuildStopsOnInvalidRevision(string revision) // CM-01
    {
        var result = Dotnet.Run(Repository.Root, ["build", CoreProject, "--no-restore", $"-p:Version={revision}"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Revision '{revision}' is not valid", result.Output, StringComparison.Ordinal);
    }

    private static string Evaluate(Dictionary<string, string?> environment)
    {
        var result = Dotnet.Run(Repository.Root, ["msbuild", CoreProject, "-getProperty:Version"], environment);

        Assert.True(result.ExitCode == 0, result.Output);
        return result.Output.Trim();
    }
}

/// <summary>A real build with a given build number (spec FT-12, CM-04, CM-05). The <c>--about</c> output comes with WP11.</summary>
public sealed class RevisionBuildTests(RevisionBuild build) : IClassFixture<RevisionBuild>, IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void VersionOptionPrintsOnlyTheFullRevision() // CM-04, REQ-38
    {
        var result = RunCli("--version");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Equal($"MissingFiles {build.Revision}", result.Output.Trim());
    }

    [Fact]
    public void ScanResultFileAndCopyLogHaveTheFullRevision() // CM-04, REQ-19, REQ-32
    {
        _folder.AddSource("IMG_0001.JPG");
        var types = _folder.WriteFile("types.json", """{ "schemaVersion": 1, "groups": [ { "name": "Pictures", "extensions": [".jpg"] } ] }""");

        var scan = RunCli("scan", "--source", _folder.Source, "--dest", _folder.Destination, "--out", _folder.Output, "--types-file", types);

        Assert.Equal((int)ExitCode.SuccessWithFindings, scan.ExitCode);
        var resultFile = Assert.Single(_folder.OutputFiles("MissingFiles_*.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(resultFile));
        Assert.Equal($"MissingFiles {build.Revision}", document.RootElement.GetProperty("application").GetString());

        var copy = RunCli("copy", "--result", resultFile, "--out", _folder.Output);

        Assert.Equal((int)ExitCode.Success, copy.ExitCode);
        var log = Assert.Single(_folder.OutputFiles("MissingFilesCopy_*.log"));
        Assert.Matches($@"(?m)^Application:\s+MissingFiles {Regex.Escape(build.Revision)}\r?$", File.ReadAllText(log));
    }

    [Theory]
    [InlineData("MissingFiles.exe")]
    [InlineData("MissingFiles.Cli.exe")]
    [InlineData("MissingFiles.dll")]
    [InlineData("MissingFiles.Cli.dll")]
    [InlineData("MissingFiles.Core.dll")]
    public void AllFilesOfTheBuildHaveTheFullRevision(string fileName) // CM-04, CM-05
    {
        var path = Path.Join(build.Output, fileName);

        var info = FileVersionInfo.GetVersionInfo(path);

        Assert.Equal(build.Revision, info.FileVersion);
        Assert.Equal(build.Revision, info.ProductVersion);
        if (fileName.EndsWith(".dll", StringComparison.Ordinal))
        {
            Assert.Equal(build.Revision, AssemblyName.GetAssemblyName(path).Version?.ToString());
        }
    }

    [Theory]
    [InlineData("MissingFiles.exe")]
    [InlineData("MissingFiles.Cli.exe")]
    [InlineData("MissingFiles.dll")]
    [InlineData("MissingFiles.Cli.dll")]
    public void EmbeddedManifestHasNoVersionOfItsOwn(string fileName) // CM-02, CM-05, NFR-11
    {
        // Without a manifest in the project, the compiler embeds its default one, with a fixed version.
        var content = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Join(build.Output, fileName)));

        Assert.Contains("requestedExecutionLevel level=\"asInvoker\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("<assemblyIdentity", content, StringComparison.Ordinal);
    }

    private CommandResult RunCli(params string[] arguments) =>
        CommandResult.Run(Path.Join(build.Output, "MissingFiles.Cli.exe"), _folder.Root, arguments);
}

/// <summary>
/// A copy of the sources built once for all tests. A copy, because a build in the repository
/// would leave the given build number in its obj folders.
/// </summary>
public sealed class RevisionBuild : IDisposable
{
    /// <summary>The highest number allowed (CM-01), so the compiler and the file version are also used at the limit.</summary>
    public const string BuildNumber = "65534";

    private readonly string _root = Path.Join(Path.GetTempPath(), "MissingFilesFunctionalTests", Guid.NewGuid().ToString("N"));

    public RevisionBuild()
    {
        try
        {
            CopyFolder(Path.Join(Repository.Root, "src"), Path.Join(_root, "src"));
            foreach (var file in new[] { "Directory.Build.props", "global.json", ".editorconfig" })
            {
                File.Copy(Path.Join(Repository.Root, file), Path.Join(_root, file));
            }

            foreach (var project in new[] { @"src\MissingFiles.App", @"src\MissingFiles.Cli" })
            {
                // The packages are already restored for the repository, so the audit is not needed and no network is used.
                var result = Dotnet.Run(_root, ["build", project, "-o", Output, $"-p:MissingFilesBuildNumber={BuildNumber}", "-p:NuGetAudit=false"]);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Build of {project} failed ({result.ExitCode}): {result.Output}");
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string Output => Path.Join(_root, "out");

    public string Revision { get; } = WithBuildNumber(BuildNumber);

    /// <summary>The XX.YY.ZZ of this build, with another build number.</summary>
    public static string WithBuildNumber(string buildNumber) =>
        $"{ProductInfo.Version[..ProductInfo.Version.LastIndexOf('.')]}.{buildNumber}";

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void CopyFolder(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Join(target, Path.GetFileName(file)));
        }

        foreach (var folder in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(folder);
            if (name is not ("bin" or "obj"))
            {
                CopyFolder(folder, Path.Join(target, name));
            }
        }
    }
}
