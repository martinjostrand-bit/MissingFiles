using MissingFiles.Core;

namespace MissingFiles.Cli;

public static class Program
{
    private const string Usage = """
        Usage:
          MissingFiles.Cli scan --source <path> --dest <path> [--out <folder>] [--types-file <file.json>] [--groups <g1,g2>]
          MissingFiles.Cli copy --result <file.json> [--out <folder>] [--dry-run]
          MissingFiles.Cli --help | --version
        """;

    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return (int)Run(args, Console.Out, Console.Error);
    }

    internal static ExitCode Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "/?")
        {
            stdout.WriteLine(ProductInfo.DisplayName);
            stdout.WriteLine(Usage);
            return args.Length == 0 ? ExitCode.InvalidArguments : ExitCode.Success;
        }

        if (args[0] == "--version")
        {
            stdout.WriteLine(ProductInfo.DisplayName);
            return ExitCode.Success;
        }

        // The scan and copy commands are implemented in WP3.
        stderr.WriteLine($"Unknown or not yet implemented command: {args[0]}");
        stderr.WriteLine(Usage);
        return ExitCode.InvalidArguments;
    }
}
