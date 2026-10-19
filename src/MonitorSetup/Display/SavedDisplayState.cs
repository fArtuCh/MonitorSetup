using System.Text.Json;

namespace MonitorSetup.Display;

/// <summary>
/// Remembers how a display was set up when it was disabled, so enabling it again
/// puts it back where it was with the same resolution and refresh rate.
/// </summary>
internal static class SavedDisplayState
{
    public sealed record Entry(int X, int Y, int Width, int Height, int RefreshRate, DisplayOrientation Orientation);

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorSetup", "disabled-displays.json");

    public static void Save(DisplayInfo display)
    {
        var all = Load();
        all[Id(display)] = new Entry(display.Bounds.X, display.Bounds.Y, display.Bounds.Width, display.Bounds.Height,
            display.RefreshRate, display.Orientation);
        Write(all);
    }

    public static Entry? Get(DisplayInfo display) => Load().GetValueOrDefault(Id(display));

    private static string Id(DisplayInfo d) => string.IsNullOrEmpty(d.DevicePath) ? d.Key : d.DevicePath;

    private static Dictionary<string, Entry> Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    private static void Write(Dictionary<string, Entry> all)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember the layout is not worth failing the change for.
        }
    }
}
