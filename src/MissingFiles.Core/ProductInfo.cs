using System.Reflection;

namespace MissingFiles.Core;

/// <summary>
/// Product name and revision, shared by the UI, the CLI and the output files.
/// </summary>
public static class ProductInfo
{
    public const string Name = "MissingFiles";

    /// <summary>Full revision XX.YY.ZZ.BB, e.g. "1.0.0.123" (spec CM-01).</summary>
    public static string Version { get; } = GetVersion();

    /// <summary>Name and revision, e.g. "MissingFiles 1.0.0.123".</summary>
    public static string DisplayName => $"{Name} {Version}";

    // The file version is what the properties of the executables show (CM-04).
    private static string GetVersion() =>
        typeof(ProductInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "0.0.0.0";
}
