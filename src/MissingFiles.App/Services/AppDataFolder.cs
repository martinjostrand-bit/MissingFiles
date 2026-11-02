using System.IO;

using MissingFiles.Core.FileTypes;

namespace MissingFiles.App.Services;

/// <summary>
/// The optional folder for the app's settings and file types file (REQ-04a), named by an
/// environment variable. The UI smoke tests use it, so the name is never shown in the UI.
/// </summary>
public static class AppDataFolder
{
    public const string VariableName = "MISSINGFILES_DATA_FOLDER";

    /// <summary>Reads a value of the variable.</summary>
    /// <param name="value">The value, or null if the variable does not exist.</param>
    /// <param name="folder">The folder, or null if the variable is not set or is empty.</param>
    /// <returns>False if the value is not an absolute path to a folder.</returns>
    public static bool TryResolve(string? value, out string? folder)
    {
        folder = string.IsNullOrEmpty(value) ? null : value;

        // A network path needs a share: "\\server" counts as fully qualified, but a file in it has no folder.
        return folder is null
            || (Path.IsPathFullyQualified(folder) && Path.GetDirectoryName(UserFileTypesPath(folder)) is not null);
    }

    /// <summary>Where the settings are stored (REQ-04).</summary>
    /// <param name="folder">The folder from <see cref="TryResolve"/>; null for the default location.</param>
    public static string SettingsPath(string? folder) =>
        folder is null ? JsonSettingsStore.DefaultPath : Path.Join(folder, "settings.json");

    /// <summary>The user's file types file (REQ-05b).</summary>
    /// <param name="folder">The folder from <see cref="TryResolve"/>; null for the default location.</param>
    public static string UserFileTypesPath(string? folder) =>
        folder is null ? FileTypesFile.UserFilePath : Path.Join(folder, "FileTypes.json");
}
