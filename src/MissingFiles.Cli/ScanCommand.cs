using MissingFiles.Core.FileTypes;
using MissingFiles.Core.Scanning;

namespace MissingFiles.Cli;

/// <summary>The <c>scan</c> command (spec REQ-33, REQ-33a).</summary>
internal static class ScanCommand
{
    public const string Usage = """
        Usage: MissingFiles.Cli scan --source <folder> --dest <folder> [options]

        Finds files in the source folder that are missing from the destination folder
        (same name and same size, in any subfolder) and writes them to a JSON result file.

        Options:
          --source <folder>       Folder to check (required).
          --dest <folder>         Folder that should contain the files (required).
          --out <folder>          Where to write the result file.
                                  Default: Documents\MissingFiles
          --types-file <file>     File types file (JSON).
                                  Default: %APPDATA%\MissingFiles\FileTypes.json,
                                  or the built-in pictures + videos list if it does not exist.
          --groups <g1,g2>        Use only these groups from the file types file,
                                  instead of the groups marked "enabled".

        Exit codes: 0 nothing missing, 1 missing files found, 2 some folders could not be read,
                    3 invalid arguments or input, 4 cancelled, 5 fatal error.
        """;

    private const int MaxErrorsShown = 20;

    public static ExitCode Run(IReadOnlyList<string> args, CliContext context)
    {
        var commandLine = CommandLine.Parse(
            args,
            valueOptions: ["source", "dest", "out", "types-file", "groups"],
            requiredOptions: ["source", "dest"]);
        if (commandLine.Help)
        {
            context.Out.WriteLine(Usage);
            return ExitCode.Success;
        }

        var (definition, fileTypesPath) = LoadFileTypes(commandLine.Get("types-file"), context);
        var filter = definition.CreateFilter(ParseGroups(commandLine.Get("groups")));
        var outputFolder = commandLine.Get("out") ?? context.DefaultOutputFolder;

        var options = new ScanOptions
        {
            SourceRoot = commandLine.Require("source"),
            DestinationRoot = commandLine.Require("dest"),
            Filter = filter,
            FileTypesFilePath = fileTypesPath,
            TimeProvider = context.TimeProvider,
        };

        context.Out.WriteLine(Format.Field("File types", $"{fileTypesPath ?? "built-in default"} ({filter.Extensions.Count} extensions)"));

        var line = new ProgressLine(context);
        ScanResult result;
        try
        {
            result = Scanner.Run(options, new SyncProgress<ScanProgress>(p => line.Update(Describe(p))), context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            line.Finish();
            context.Error.WriteLine("Scan cancelled. No result file was written.");
            return ExitCode.Cancelled;
        }

        line.Finish();
        var resultPath = ScanResultFile.Save(result, outputFolder);
        WriteSummary(context, result, resultPath);
        return ExitCodeFor(result.Summary);
    }

    /// <summary>Exit code for a completed scan (REQ-35).</summary>
    internal static ExitCode ExitCodeFor(ScanSummary summary) =>
        summary.Errors > 0 ? ExitCode.CompletedWithErrors
        : summary.Missing > 0 ? ExitCode.SuccessWithFindings
        : ExitCode.Success;

    private static (FileTypesDefinition Definition, string? Path) LoadFileTypes(string? typesFile, CliContext context)
    {
        if (typesFile is not null)
        {
            var path = Path.GetFullPath(typesFile);
            return (FileTypesFile.Load(path), path);
        }

        return File.Exists(context.UserFileTypesPath)
            ? (FileTypesFile.Load(context.UserFileTypesPath), context.UserFileTypesPath)
            : (FileTypesFile.LoadDefault(), null);
    }

    private static string[]? ParseGroups(string? groups)
    {
        if (groups is null)
        {
            return null;
        }

        var names = groups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return names.Length > 0 ? names : throw new CliArgumentException("Option --groups needs at least one group name.");
    }

    private static string Describe(ScanProgress p) => p.Phase switch
    {
        ScanPhase.IndexingDestination =>
            $"Reading destination: {Format.Count(p.DestinationFilesIndexed)} files | {Format.Duration(p.Elapsed)}",
        _ =>
            $"Scanning source: {Format.Count(p.SourceFilesScanned)} files, {Format.Count(p.Matched)} matched, {Format.Count(p.Missing)} missing | {Format.Duration(p.Elapsed)}",
    };

    private static void WriteSummary(CliContext context, ScanResult result, string resultPath)
    {
        var s = result.Summary;
        var output = context.Out;

        output.WriteLine();
        output.WriteLine($"Scan finished in {Format.Duration(result.Duration)}");
        output.WriteLine(Format.Field("Source", result.SourceRoot));
        output.WriteLine(Format.Field("Destination", result.DestinationRoot));
        output.WriteLine(Format.Field("Source files scanned", Format.Count(s.SourceFilesScanned)));
        output.WriteLine(Format.Field("Matched", Format.Count(s.Matched)));
        output.WriteLine(Format.Field("Missing", Format.Count(s.Missing)));
        if (s.SameNameDifferentSize > 0)
        {
            output.WriteLine(Format.Field("  same name, other size", Format.Count(s.SameNameDifferentSize)));
        }

        output.WriteLine(Format.Field("Errors", Format.Count(s.Errors)));
        if (s.DuplicateNameGroups > 0)
        {
            output.WriteLine(Format.Field("Duplicate names", $"{Format.Count(s.DuplicateNameGroups)} (file names that occur more than once in the source)"));
        }

        output.WriteLine(Format.Field("Result file", resultPath));

        if (result.Errors.Count > 0)
        {
            context.Error.WriteLine($"{result.Errors.Count} folder(s) could not be read:");
            foreach (var error in result.Errors.Take(MaxErrorsShown))
            {
                context.Error.WriteLine($"  {error.Path}: {error.Message}");
            }

            if (result.Errors.Count > MaxErrorsShown)
            {
                context.Error.WriteLine($"  ... and {result.Errors.Count - MaxErrorsShown} more (see the result file).");
            }
        }
    }
}
