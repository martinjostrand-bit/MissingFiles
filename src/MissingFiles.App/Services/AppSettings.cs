using System.IO;
using System.Text.Json;

namespace MissingFiles.App.Services;

/// <summary>Values remembered between sessions (spec REQ-04, REQ-05c).</summary>
public sealed record AppSettings
{
    public string? SourceFolder { get; init; }

    public string? DestinationFolder { get; init; }

    public string? OutputFolder { get; init; }

    public string? FileTypesFile { get; init; }
}

public interface ISettingsStore
{
    /// <summary>Returns the saved settings, or empty settings if none are saved or the file is unreadable.</summary>
    AppSettings Load();

    /// <summary>Saves the settings; failures are ignored, as settings are a convenience.</summary>
    void Save(AppSettings settings);
}

/// <summary>Stores settings as JSON, by default in <c>%APPDATA%\MissingFiles\settings.json</c>.</summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MissingFiles", "settings.json");

    public AppSettings Load()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember the folders is not worth interrupting the user for.
        }
    }
}
