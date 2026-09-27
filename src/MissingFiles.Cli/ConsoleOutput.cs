using System.Globalization;

namespace MissingFiles.Cli;

/// <summary>
/// Shows progress: on one updating line in a console window, or as a line every
/// few seconds when the output is redirected to a file or another program.
/// </summary>
internal sealed class ProgressLine(CliContext context)
{
    private static readonly TimeSpan RedirectedInterval = TimeSpan.FromSeconds(5);

    private readonly long _startTimestamp = context.TimeProvider.GetTimestamp();
    private TimeSpan _lastRedirectedLine;
    private int _lastLength;

    public void Update(string text)
    {
        if (context.Interactive)
        {
            // Overwrite the previous text completely, including when the new text is shorter.
            context.Out.Write("\r" + text.PadRight(_lastLength));
            _lastLength = text.Length;
        }
        else
        {
            var elapsed = context.TimeProvider.GetElapsedTime(_startTimestamp);
            if (elapsed - _lastRedirectedLine >= RedirectedInterval)
            {
                _lastRedirectedLine = elapsed;
                context.Out.WriteLine(text);
            }
        }
    }

    /// <summary>Ends the progress line so that following output starts on a new line.</summary>
    public void Finish()
    {
        if (_lastLength > 0)
        {
            context.Out.WriteLine();
            _lastLength = 0;
        }
    }
}

/// <summary>Calls an action synchronously on the reporting thread.</summary>
internal sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

internal static class Format
{
    public static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Duration(TimeSpan value) => value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    /// <summary>A label padded to a fixed width, so values line up.</summary>
    public static string Field(string label, string value) => $"  {(label + ": ").PadRight(28)}{value}";
}
