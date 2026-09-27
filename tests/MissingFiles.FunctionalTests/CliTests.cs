using System.Diagnostics;

using MissingFiles.Cli;
using MissingFiles.Core;

namespace MissingFiles.FunctionalTests;

/// <summary>General CLI behaviour (spec FT-09).</summary>
public sealed class CliTests : IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void VersionPrintsDisplayNameAndSucceeds()
    {
        var run = Cli.Run(_folder, ["--version"]);

        Assert.Equal(ExitCode.Success, run.Code);
        Assert.Contains(ProductInfo.DisplayName, run.Out, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    [InlineData("help")]
    public void HelpPrintsUsageAndSucceeds(string option)
    {
        var run = Cli.Run(_folder, [option]);

        Assert.Equal(ExitCode.Success, run.Code);
        Assert.Contains("Usage:", run.Out, StringComparison.Ordinal);
        Assert.Contains("MissingFiles.Cli scan", run.Out, StringComparison.Ordinal);
        Assert.Contains("MissingFiles.Cli copy", run.Out, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("scan", "--source <folder>")]
    [InlineData("copy", "--dry-run")]
    public void CommandHelpDescribesOptionsAndExitCodes(string command, string expected)
    {
        var run = Cli.Run(_folder, [command, "--help"]);

        Assert.Equal(ExitCode.Success, run.Code);
        Assert.Contains(expected, run.Out, StringComparison.Ordinal);
        Assert.Contains("Exit codes:", run.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void NoArgumentsIsInvalid()
    {
        var run = Cli.Run(_folder, []);

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains("Usage:", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCommandIsInvalid()
    {
        var run = Cli.Run(_folder, ["frobnicate"]);

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains("Unknown command 'frobnicate'", run.Error, StringComparison.Ordinal);
        Assert.Contains("--help", run.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new[] { "scan", "--source", "x" }, "Missing required option --dest")]
    [InlineData(new[] { "scan", "--source", "x", "--dest" }, "Option --dest needs a value")]
    [InlineData(new[] { "scan", "--source", "x", "--source", "y", "--dest", "z" }, "Option --source is given more than once")]
    [InlineData(new[] { "scan", "--source", "x", "--dest", "y", "--colour", "red" }, "Unknown option --colour")]
    [InlineData(new[] { "scan", "--source", "x", "--dest", "y", "extra" }, "Unexpected argument 'extra'")]
    [InlineData(new[] { "copy" }, "Missing required option --result")]
    [InlineData(new[] { "copy", "--result", "r.json", "--dry-run=yes" }, "Option --dry-run does not take a value")]
    public void InvalidArgumentsAreReported(string[] args, string expected) // FT-09
    {
        var run = Cli.Run(_folder, args);

        Assert.Equal(ExitCode.InvalidArguments, run.Code);
        Assert.Contains(expected, run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void RealProcessReturnsExitCode() // FT-09: Main wiring and exit code of the process
    {
        _folder.AddSource("IMG_0001.JPG");
        var cli = Path.Join(AppContext.BaseDirectory, "MissingFiles.Cli.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { cli, "scan", "--source", _folder.Source, "--dest", _folder.Destination, "--out", _folder.Output },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.Equal((int)ExitCode.SuccessWithFindings, process.ExitCode);
        Assert.Contains("Missing:", output, StringComparison.Ordinal);
        Assert.Single(_folder.OutputFiles("MissingFiles_*.json"));
    }
}
