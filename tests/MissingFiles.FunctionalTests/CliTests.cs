using MissingFiles.Cli;
using MissingFiles.Core;

namespace MissingFiles.FunctionalTests;

public class CliTests
{
    [Fact]
    public void VersionPrintsDisplayNameAndSucceeds()
    {
        var (code, stdout, _) = RunCli("--version");

        Assert.Equal(ExitCode.Success, code);
        Assert.Contains(ProductInfo.DisplayName, stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpPrintsUsageAndSucceeds()
    {
        var (code, stdout, _) = RunCli("--help");

        Assert.Equal(ExitCode.Success, code);
        Assert.Contains("Usage:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void NoArgumentsIsInvalid()
    {
        var (code, _, _) = RunCli();

        Assert.Equal(ExitCode.InvalidArguments, code);
    }

    [Fact]
    public void UnknownCommandIsInvalid()
    {
        var (code, _, stderr) = RunCli("frobnicate");

        Assert.Equal(ExitCode.InvalidArguments, code);
        Assert.Contains("frobnicate", stderr, StringComparison.Ordinal);
    }

    private static (ExitCode Code, string Stdout, string Stderr) RunCli(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = Program.Run(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }
}
