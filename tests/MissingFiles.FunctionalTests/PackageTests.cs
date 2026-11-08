using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;

using MissingFiles.Cli;

namespace MissingFiles.FunctionalTests;

/// <summary>The release zip built by <c>build\Package.ps1</c> (spec NFR-10, NFR-11, CM-06).</summary>
public sealed class PackageTests(ReleasePackage package) : IClassFixture<ReleasePackage>
{
    /// <summary>The files of the zip (NFR-10), in ordinal order.</summary>
    internal static readonly string[] ZipContent = ["FileTypes.default.json", "LICENSE", "MissingFiles.Cli.exe", "MissingFiles.exe"];

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void ZipIsNamedAfterTheRevision() // CM-06, NFR-10
    {
        var zipName = $"MissingFiles-{package.Revision}-win-x64.zip";

        Assert.Equal(new[] { zipName, "publish" }, Directory.GetFileSystemEntries(package.Output).Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal));
        Assert.Equal(Path.Join(package.Output, zipName), package.Result.Output.TrimEnd().Split('\n')[^1].TrimEnd('\r'));
    }

    [Fact]
    public void ZipHoldsOnlyTheExecutablesTheFileTypesFileAndTheLicense() // NFR-10
    {
        using var zip = ZipFile.OpenRead(package.ZipPath);

        // Only files, no folders: the executables are single files with nothing next to them.
        Assert.Equal(ZipContent, zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("FileTypes.default.json", @"src\MissingFiles.Core\FileTypes\FileTypes.default.json")] // REQ-05b
    [InlineData("LICENSE", "LICENSE")]
    public void ShippedFilesAreThoseOfTheRepository(string fileName, string repositoryFile) // NFR-10
    {
        Assert.Equal(File.ReadAllBytes(Path.Join(Repository.Root, repositoryFile)), File.ReadAllBytes(Path.Join(package.Extracted, fileName)));
    }

    [Theory]
    [InlineData("MissingFiles.exe", "Microsoft.WindowsDesktop.App")]
    [InlineData("MissingFiles.Cli.exe", "Microsoft.NETCore.App")]
    public void ExecutablesAreSelfContained(string fileName, string framework) // NFR-10
    {
        // The runtime configuration in the bundle lists the frameworks that are included in the file,
        // instead of the installed frameworks that a framework-dependent application needs.
        var content = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Join(package.Extracted, fileName)));

        var included = new Regex(@"""includedFrameworks"":\s*\[(?<list>[^\]]*)\]", RegexOptions.None, RegexTimeout).Match(content);
        Assert.True(included.Success, $"{fileName} has no included frameworks, so it is not self-contained.");
        Assert.Contains($"\"name\": \"{framework}\"", included.Groups["list"].Value, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"""frameworks?"":\s*[\[{]", RegexOptions.None, RegexTimeout), content);
    }

    [Theory]
    [InlineData("MissingFiles.exe")]
    [InlineData("MissingFiles.Cli.exe")]
    public void ExecutablesAreX64(string fileName) // NFR-10, §3.3
    {
        using var stream = File.OpenRead(Path.Join(package.Extracted, fileName));
        using var reader = new PEReader(stream);

        Assert.Equal(Machine.Amd64, reader.PEHeaders.CoffHeader.Machine);
    }

    [Theory]
    [InlineData("MissingFiles.exe")]
    [InlineData("MissingFiles.Cli.exe")]
    public void ExecutablesHaveTheRevisionOfTheZip(string fileName) // CM-04, CM-05, CM-06
    {
        var info = FileVersionInfo.GetVersionInfo(Path.Join(package.Extracted, fileName));

        Assert.Equal(package.Revision, info.FileVersion);
        Assert.Equal(package.Revision, info.ProductVersion);
    }

    [Theory]
    [InlineData("MissingFiles.exe")]
    [InlineData("MissingFiles.Cli.exe")]
    public void ExecutablesNeedNoAdministratorRights(string fileName) // NFR-11
    {
        var content = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Join(package.Extracted, fileName)));

        Assert.Contains("requestedExecutionLevel level=\"asInvoker\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("requireAdministrator", content, StringComparison.Ordinal);
        Assert.DoesNotContain("highestAvailable", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedCommandLineScansWithTheShippedFileTypesFile() // NFR-10, REQ-05b
    {
        using var folder = new TestFolder();
        folder.AddSource(@"2021\IMG_0001.JPG");
        folder.AddSource("notes.txt");

        var result = CommandResult.Run(
            Path.Join(package.Extracted, "MissingFiles.Cli.exe"),
            folder.Root,
            ["scan", "--source", folder.Source, "--dest", folder.Destination, "--out", folder.Output,
             "--types-file", Path.Join(package.Extracted, "FileTypes.default.json")]);

        Assert.True(result.ExitCode == (int)ExitCode.SuccessWithFindings, result.Output);
        Assert.Contains($"MissingFiles {package.Revision}", File.ReadAllText(Assert.Single(folder.OutputFiles("MissingFiles_*.json"))), StringComparison.Ordinal);
        Assert.Matches(@"(?m)^  Missing:\s+1\r?$", result.Output);
    }

    [Fact]
    public void PackagingStopsIfTheOutputFolderIsNotEmpty() // NFR-05
    {
        var output = package.NewFolder();
        var kept = Path.Join(output, "keep.txt");
        File.WriteAllText(kept, "keep");

        var result = package.Run(output);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"The output folder '{output}' is not empty", result.Output, StringComparison.Ordinal);
        Assert.Equal(new[] { kept }, Directory.GetFileSystemEntries(output));
        Assert.Equal("keep", File.ReadAllText(kept));
    }

    [Fact]
    public void PackagingStopsIfTheBuildFails() // CM-01
    {
        var output = package.NewFolder();

        var result = package.Run(output, new Dictionary<string, string?> { ["MissingFilesBuildNumber"] = "65535" });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Revision '1.0.0.65535' is not valid", result.Output, StringComparison.Ordinal);
        Assert.Contains("dotnet publish of MissingFiles.App failed", result.Output, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(output, "*.zip", SearchOption.AllDirectories));
    }

    [Fact]
    public void PackagingStopsOnAFileThatWouldNotBeInTheZip() // NFR-10
    {
        var output = package.NewFolder();
        var extension = package.NewFolder();
        File.WriteAllText(Path.Join(extension, "Unexpected.dll"), "x");
        var targets = WriteTargets(extension, """
            <ItemGroup Condition="'$(MSBuildProjectName)' == 'MissingFiles.Cli'">
              <None Include="$(MSBuildThisFileDirectory)Unexpected.dll" CopyToPublishDirectory="PreserveNewest" ExcludeFromSingleFile="true" />
            </ItemGroup>
            """);

        var result = package.Run(output, new Dictionary<string, string?> { ["CustomAfterMicrosoftCommonTargets"] = targets });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unexpected: Unexpected.dll. Missing: none.", result.Output, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(output, "*.zip", SearchOption.AllDirectories));
    }

    [Fact]
    public void PackagingStopsIfTheExecutablesHaveDifferentRevisions() // CM-05
    {
        var output = package.NewFolder();
        var targets = WriteTargets(package.NewFolder(), """
            <PropertyGroup Condition="'$(MSBuildProjectName)' == 'MissingFiles.Cli'">
              <Version>1.0.0.1</Version>
            </PropertyGroup>
            """);

        var result = package.Run(output, new Dictionary<string, string?> { ["CustomAfterMicrosoftCommonTargets"] = targets });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"The executables do not have one four-part revision (CM-01, CM-05): {package.Revision}, 1.0.0.1.", result.Output, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(output, "*.zip", SearchOption.AllDirectories));
    }

    [Fact]
    public void CiWorkflowUploadsTheReleaseZip() // CM-06
    {
        var workflow = File.ReadAllText(Path.Join(Repository.Root, ".github", "workflows", "ci.yml"));

        Assert.Matches(@"(?m)^        run: \./build/Package\.ps1 -Output artifacts\r?$", workflow);
        Assert.Matches(@"(?m)^          path: artifacts/MissingFiles-\*-win-x64\.zip\r?$", workflow);
    }

    /// <summary>An MSBuild file that every project of the packaging imports, through CustomAfterMicrosoftCommonTargets.</summary>
    private static string WriteTargets(string folder, string content)
    {
        var path = Path.Join(folder, "Test.targets");
        File.WriteAllText(path, $"<Project>{Environment.NewLine}{content}{Environment.NewLine}</Project>");
        return path;
    }
}

/// <summary>
/// The release zip, built once for all tests with <c>build\Package.ps1</c> from a copy of the sources (<see cref="SourceCopy"/>),
/// and extracted. Windows PowerShell runs the script here, PowerShell 7 in CI.
/// </summary>
/// <remarks>
/// A self-contained publish needs the runtime packs. NuGet downloads them on the first run on a machine,
/// for example in CI, where the tests run before the release zip is built.
/// </remarks>
public sealed class ReleasePackage : IDisposable
{
    /// <summary>Another build number than the revision tests use, so the zip name shows where it comes from.</summary>
    public const string BuildNumber = "4321";

    private readonly string _root = Path.Join(Path.GetTempPath(), "MissingFilesFunctionalTests", Guid.NewGuid().ToString("N"));
    private int _folders;

    public ReleasePackage()
    {
        try
        {
            SourceCopy.Create(Sources);
            Result = Run(Output);
            if (Result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Package.ps1 failed ({Result.ExitCode}): {Result.Output}");
            }

            ZipFile.ExtractToDirectory(ZipPath, Extracted);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string Sources => Path.Join(_root, "sources");

    public string Output => Path.Join(_root, "out");

    public string Extracted => Path.Join(_root, "extracted");

    public string Revision { get; } = RevisionBuild.WithBuildNumber(BuildNumber);

    public string ZipPath => Path.Join(Output, $"MissingFiles-{Revision}-win-x64.zip");

    /// <summary>The run that built the zip.</summary>
    internal CommandResult Result { get; }

    /// <summary>A new empty folder, deleted with the fixture.</summary>
    public string NewFolder() =>
        Directory.CreateDirectory(Path.Join(_root, $"folder{Interlocked.Increment(ref _folders)}")).FullName;

    /// <summary>Runs the script of the copy, with its output in <paramref name="output"/>.</summary>
    internal CommandResult Run(string output, IReadOnlyDictionary<string, string?>? environment = null)
    {
        var variables = new Dictionary<string, string?>
        {
            ["MissingFilesBuildNumber"] = BuildNumber,

            // The packages are already restored for the repository, so the audit is not needed.
            ["NuGetAudit"] = "false",

            // No build servers, which would outlive the script and keep its output open.
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["UseSharedCompilation"] = "false",
        };
        foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
        {
            variables[name] = value;
        }

        return CommandResult.Run(
            "powershell.exe",
            Sources,
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Join(Sources, "build", "Package.ps1"), "-Output", output],
            variables);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
