using System.Text.Json;
using MonitorSetup.Core.Abstractions;

namespace MonitorSetup.Infrastructure.Windows.Persistence;

/// <summary>Keeps saved display states in %LOCALAPPDATA%\MonitorSetup\disabled-displays.json.</summary>
internal sealed class JsonDisplayStateStore(string filePath) : IDisplayStateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorSetup", "disabled-displays.json");

    public void Save(string displayKey, SavedDisplayState state)
    {
        var all = Load();
        all[displayKey] = state;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(all, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not remembering the layout is not worth failing the change for.
        }
    }

    public SavedDisplayState? Get(string displayKey) => Load().GetValueOrDefault(displayKey);

    private Dictionary<string, SavedDisplayState> Load()
    {
        try
        {
            return File.Exists(filePath)
                ? JsonSerializer.Deserialize<Dictionary<string, SavedDisplayState>>(File.ReadAllText(filePath)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
