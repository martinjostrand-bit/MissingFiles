using System.Reflection;

namespace MissingFiles.Core;

/// <summary>
/// Product name, revision and About information, shared by the UI, the CLI and the output files (spec REQ-36, REQ-39).
/// </summary>
public static class ProductInfo
{
    public const string Name = "MissingFiles";

    public const string Author = "Martin Strand";

    public const string Copyright = "Copyright © 2026 Martin Strand";

    /// <summary>Shown before the license text (REQ-36).</summary>
    public const string LicenseStatement = "Free to use.";

    private const string LicenseResourceName = "MissingFiles.Core.LICENSE";

    private static readonly Lazy<string> License = new(ReadLicense);

    /// <summary>Full revision XX.YY.ZZ.BB, e.g. "1.0.0.123" (spec CM-01).</summary>
    public static string Version { get; } = GetVersion();

    /// <summary>Name and revision, e.g. "MissingFiles 1.0.0.123".</summary>
    public static string DisplayName => $"{Name} {Version}";

    /// <summary>
    /// The full MIT license text of the <c>LICENSE</c> file, embedded in the assembly so it is available
    /// offline and without the zip (REQ-39). Lines end with <see cref="Environment.NewLine"/>.
    /// </summary>
    public static string LicenseText => License.Value;

    /// <summary>All About information (REQ-36), as shown by the About dialog and <c>--about</c>.</summary>
    public static string AboutText => string.Join(
        Environment.NewLine,
        Name,
        $"Revision: {Version}",
        $"Author: {Author}",
        Copyright,
        string.Empty,
        $"License: {LicenseStatement}",
        string.Empty,
        LicenseText);

    // The file version is what the properties of the executables show (CM-04).
    private static string GetVersion() =>
        typeof(ProductInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "0.0.0.0";

    private static string ReadLicense()
    {
        using var stream = typeof(ProductInfo).Assembly.GetManifestResourceStream(LicenseResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {LicenseResourceName} is missing.");
        using var reader = new StreamReader(stream);

        // The same text whatever line endings the file had when it was built.
        var lines = reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd().Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line => line.TrimEnd()));
    }
}
