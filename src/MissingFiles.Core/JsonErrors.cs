using System.Text.Json;

namespace MissingFiles.Core;

internal static class JsonErrors
{
    /// <summary>Formats a JSON error with a 1-based line and column (REQ-05d).</summary>
    public static string Describe(JsonException ex)
    {
        // The JsonException message ends with " Path: ... | LineNumber: ... | BytePositionInLine: ...".
        var reason = ex.Message;
        var pathIndex = reason.IndexOf(" Path:", StringComparison.Ordinal);
        if (pathIndex > 0)
        {
            reason = reason[..pathIndex];
        }

        return ex.LineNumber is { } line
            ? $"Invalid JSON at line {line + 1}, column {(ex.BytePositionInLine ?? 0) + 1}: {reason}"
            : $"Invalid JSON: {reason}";
    }
}
