using System.Diagnostics;
using System.Globalization;

using MissingFiles.Core.Scanning;

using Xunit.Abstractions;

namespace MissingFiles.FunctionalTests;

/// <summary>Runs only when <c>MISSINGFILES_PERF=1</c>, i.e. nightly or manually (FT-10).</summary>
internal sealed class PerformanceFactAttribute : FactAttribute
{
    public PerformanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MISSINGFILES_PERF") is not ("1" or "true"))
        {
            Skip = "Performance test (FT-10); set MISSINGFILES_PERF=1 to run it.";
        }
    }
}

/// <summary>
/// FT-10: a synthetic tree of zero-byte files on each side, scanned by the real CLI process,
/// checking scan time (NFR-01) and memory (NFR-03).
/// </summary>
/// <remarks>
/// Settings (environment variables): <c>MISSINGFILES_PERF_FILES</c> files per side (default 1,000,000),
/// <c>MISSINGFILES_PERF_MAX_SECONDS</c> time limit (default 120, NFR-01 for a local SSD),
/// <c>MISSINGFILES_PERF_MAX_MB</c> memory limit (default 500, NFR-03).
/// </remarks>
[Trait("Category", "Performance")]
public sealed class PerformanceTests(ITestOutputHelper output) : IDisposable
{
    private const int FilesPerFolder = 1000;
    private const int MissingEvery = 100;

    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [PerformanceFact]
    public void ScanOfOneMillionFilesPerSideMeetsTimeAndMemoryLimits()
    {
        var files = Setting("MISSINGFILES_PERF_FILES", 1_000_000);
        var maxSeconds = Setting("MISSINGFILES_PERF_MAX_SECONDS", 120);
        var maxMegabytes = Setting("MISSINGFILES_PERF_MAX_MB", 500);

        var generate = Stopwatch.StartNew();
        var expectedMissing = Generate(files);
        output.WriteLine($"Generated {files:N0} files per side in {generate.Elapsed}.");

        var (elapsed, peakBytes, exitCode) = RunCliScan();
        var peakMegabytes = peakBytes / (1024 * 1024);
        output.WriteLine($"Scan took {elapsed}, peak working set {peakMegabytes:N0} MB.");

        Assert.Equal(1, exitCode); // missing files found
        var result = ScanResultFile.Load(Assert.Single(_folder.OutputFiles("MissingFiles_*.json")));
        Assert.Equal(files, result.Summary.SourceFilesScanned);
        Assert.Equal(expectedMissing, result.Summary.Missing);
        Assert.Equal(files - expectedMissing, result.Summary.Matched);
        Assert.True(elapsed < TimeSpan.FromSeconds(maxSeconds), $"NFR-01: scan took {elapsed}, limit {maxSeconds} s.");
        Assert.True(peakMegabytes < maxMegabytes, $"NFR-03: peak working set {peakMegabytes} MB, limit {maxMegabytes} MB.");
    }

    /// <summary>
    /// Source: generated names in folders of 1,000. Destination: the same files in another folder
    /// layout, except every 100th. Returns the number of missing files.
    /// </summary>
    private int Generate(int files)
    {
        var names = TestData.Names(files);
        var folders = (files + FilesPerFolder - 1) / FilesPerFolder;
        Parallel.For(0, folders, folder =>
        {
            var source = Directory.CreateDirectory(Path.Join(_folder.Source, $"{2000 + (folder / 100)}", $"Card {folder:D4}")).FullName;
            var destination = Directory.CreateDirectory(Path.Join(_folder.Destination, "Archive", $"Batch {folder % 500:D3}", $"{folder:D4}")).FullName;
            for (var i = folder * FilesPerFolder; i < Math.Min(files, (folder + 1) * FilesPerFolder); i++)
            {
                File.Create(Path.Join(source, names[i])).Dispose();
                if (i % MissingEvery != 0)
                {
                    File.Create(Path.Join(destination, names[i])).Dispose();
                }
            }
        });

        return (files + MissingEvery - 1) / MissingEvery;
    }

    /// <summary>Runs the CLI as its own process, like a user would, measuring time and peak memory.</summary>
    private (TimeSpan Elapsed, long PeakWorkingSet, int ExitCode) RunCliScan()
    {
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList =
            {
                Path.Join(AppContext.BaseDirectory, "MissingFiles.Cli.dll"),
                "scan", "--source", _folder.Source, "--dest", _folder.Destination, "--out", _folder.Output,
            },
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        var watch = Stopwatch.StartNew();
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        long peak = 0;
        while (!process.WaitForExit(100))
        {
            try
            {
                process.Refresh();
                peak = Math.Max(peak, process.PeakWorkingSet64);
            }
            catch (InvalidOperationException)
            {
                // The process ended between the checks.
            }
        }

        watch.Stop();
        output.WriteLine(stdout.Result);
        return (watch.Elapsed, peak, process.ExitCode);
    }

    private static int Setting(string name, int defaultValue) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
}
