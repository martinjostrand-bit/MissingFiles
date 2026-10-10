using System.Globalization;

using MissingFiles.Cli;

namespace MissingFiles.FunctionalTests;

/// <summary>
/// A temporary folder with source, destination, output and settings subfolders, deleted on dispose.
/// </summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Root = Path.Join(Path.GetTempPath(), "MissingFilesFunctionalTests", Guid.NewGuid().ToString("N"));
        Source = Directory.CreateDirectory(Path.Join(Root, "source")).FullName;
        Destination = Directory.CreateDirectory(Path.Join(Root, "destination")).FullName;
        Output = Path.Join(Root, "out");
        UserFileTypes = Path.Join(Root, "appdata", "FileTypes.json");
    }

    public string Root { get; }

    public string Source { get; }

    public string Destination { get; }

    /// <summary>Output folder for result files and logs; not created in advance.</summary>
    public string Output { get; }

    /// <summary>Stands in for %APPDATA%\MissingFiles\FileTypes.json; not created in advance.</summary>
    public string UserFileTypes { get; }

    public string AddSource(string relativePath, int size = 10) => CreateFile(Source, relativePath, size);

    public string AddDestination(string relativePath, int size = 10) => CreateFile(Destination, relativePath, size);

    public void AddBoth(string relativePath, int size = 10)
    {
        AddSource(relativePath, size);
        AddDestination(relativePath, size);
    }

    public string WriteFile(string relativePath, string content)
    {
        var path = Path.Join(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string[] OutputFiles(string pattern) =>
        Directory.Exists(Output) ? Directory.GetFiles(Output, pattern) : [];

    /// <summary>Adds generated files (FT-01) to the source, each with its own size; returns their relative paths.</summary>
    public IReadOnlyList<string> AddSourceFiles(IEnumerable<string> relativePaths) =>
        [.. relativePaths.Select(p => { AddSource(p, TestData.SizeOf(p)); return p; })];

    /// <summary>Creates a file with content from <see cref="TestData.Content"/>, so equal names and sizes give equal files.</summary>
    public static string CreateFile(string root, string relativePath, int size = 10)
    {
        var path = Path.Join(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, TestData.Content(relativePath, size));
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

/// <summary>
/// A clock whose timestamp advances one second on every reading, so that every
/// rate-limited progress check reports. Wall-clock time is the real time.
/// </summary>
internal sealed class SteppingTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp += TimeSpan.TicksPerSecond;
}

/// <summary>Result of a CLI run.</summary>
internal sealed record CliRun(ExitCode Code, string Out, string Error);

internal static class Cli
{
    /// <summary>Runs the CLI in-process with output captured and settings redirected to the test folder.</summary>
    public static CliRun Run(
        TestFolder folder,
        string[] args,
        bool interactive = false,
        TimeProvider? timeProvider = null,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        using var output = new ObservedWriter(onOutput);
        using var error = new StringWriter();
        var context = new CliContext(output, error, cancellationToken)
        {
            Interactive = interactive,
            UserFileTypesPath = folder.UserFileTypes,
            DefaultOutputFolder = folder.Output,
            TimeProvider = timeProvider ?? TimeProvider.System,
        };

        var code = Program.Run(args, context);
        return new CliRun(code, output.ToString(), error.ToString());
    }

    /// <summary>Runs <c>scan</c> on the test folder's source and destination.</summary>
    public static CliRun Scan(TestFolder folder, params string[] extraArgs) =>
        Run(folder, ["scan", "--source", folder.Source, "--dest", folder.Destination, .. extraArgs]);
}

/// <summary>Captures output and passes each piece to an observer, e.g. to cancel at a certain point.</summary>
internal sealed class ObservedWriter(Action<string>? onWrite) : StringWriter(CultureInfo.InvariantCulture)
{
    public override void Write(string? value)
    {
        base.Write(value);
        if (value is not null)
        {
            onWrite?.Invoke(value);
        }
    }
}
