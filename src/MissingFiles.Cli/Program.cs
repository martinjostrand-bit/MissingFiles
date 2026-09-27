using System.Text;

using MissingFiles.Core;
using MissingFiles.Core.Copying;
using MissingFiles.Core.FileTypes;
using MissingFiles.Core.Scanning;

namespace MissingFiles.Cli;

public static class Program
{
    internal const string Usage = """
        Usage:
          MissingFiles.Cli scan --source <folder> --dest <folder> [--out <folder>] [--types-file <file.json>] [--groups <g1,g2>]
          MissingFiles.Cli copy --result <file.json> [--out <folder>] [--dry-run]
          MissingFiles.Cli --help | --version

        Run 'MissingFiles.Cli scan --help' or 'MissingFiles.Cli copy --help' for details.
        Press Ctrl+C to cancel a running scan or copy.
        """;

    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // File names may contain any characters (e.g. æøå).
        Console.OutputEncoding = Encoding.UTF8;

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            // First Ctrl+C: cancel cleanly. Second Ctrl+C: let the process end immediately.
            if (!cancellation.IsCancellationRequested)
            {
                e.Cancel = true;
                Console.Error.WriteLine();
                Console.Error.WriteLine("Cancelling... (press Ctrl+C again to stop immediately)");
                cancellation.Cancel();
            }
        };

        var context = new CliContext(Console.Out, Console.Error, cancellation.Token)
        {
            Interactive = !Console.IsOutputRedirected,
        };
        return (int)Run(args, context);
    }

    internal static ExitCode Run(IReadOnlyList<string> args, CliContext context)
    {
        if (args.Count == 0)
        {
            context.Error.WriteLine(ProductInfo.DisplayName);
            context.Error.WriteLine(Usage);
            return ExitCode.InvalidArguments;
        }

        try
        {
            switch (args[0])
            {
                case "--help" or "-h" or "/?" or "help":
                    context.Out.WriteLine(ProductInfo.DisplayName);
                    context.Out.WriteLine(Usage);
                    return ExitCode.Success;
                case "--version":
                    context.Out.WriteLine(ProductInfo.DisplayName);
                    return ExitCode.Success;
                case "scan":
                    return ScanCommand.Run(args.Skip(1).ToList(), context);
                case "copy":
                    return CopyCommand.Run(args.Skip(1).ToList(), context);
                default:
                    throw new CliArgumentException($"Unknown command '{args[0]}'.");
            }
        }
        catch (CliArgumentException ex)
        {
            context.Error.WriteLine($"Error: {ex.Message}");
            context.Error.WriteLine("Run 'MissingFiles.Cli --help' for usage.");
            return ExitCode.InvalidArguments;
        }
        catch (Exception ex) when (ex is FileTypesException or ScanValidationException or ScanResultFileException or CopyValidationException)
        {
            context.Error.WriteLine($"Error: {ex.Message}");
            return ExitCode.InvalidArguments;
        }
#pragma warning disable CA1031 // Last-resort handler: report any unexpected error with exit code 5 instead of crashing.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            context.Error.WriteLine($"Fatal error: {ex}");
            return ExitCode.FatalError;
        }
    }
}
