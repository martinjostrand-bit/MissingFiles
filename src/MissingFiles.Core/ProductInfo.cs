using System.Reflection;

namespace MissingFiles.Core;

/// <summary>
/// Product name and version, shared by the UI, the CLI and the output files.
/// </summary>
public static class ProductInfo
{
    public const string Name = "MissingFiles";

    public static string Version { get; } = GetVersion();

    /// <summary>Name and version, e.g. "MissingFiles 0.1.0".</summary>
    public static string DisplayName => $"{Name} {Version}";

    private static string GetVersion()
    {
        var informational = typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Strip the "+<commit hash>" suffix added by the SDK.
        return informational?.Split('+')[0] ?? "0.0.0";
    }
}
