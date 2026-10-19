using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;

namespace MonitorSetup.App.Cli.Commands;

internal sealed class ListCommand(IDisplayReader reader) : ICliCommand
{
    public string Name => "list";
    public string Usage => "";
    public string Description => "Show all connected displays and their numbers";

    public int Execute(IReadOnlyList<string> args, TextWriter output)
    {
        foreach (var d in reader.GetDisplays())
            output.WriteLine($"{d.Number}. {d.FriendlyName} [{d.Connection}] {d.GdiName} - {Describe(d)}");
        return 0;
    }

    private static string Describe(DisplayInfo d)
    {
        if (!d.IsActive)
            return "disabled";
        var parts = new List<string>
        {
            $"{d.Bounds.Width}x{d.Bounds.Height} @ {d.RefreshRate} Hz at ({d.Bounds.X},{d.Bounds.Y})",
            d.Orientation.ToString(),
        };
        if (d.Scale is { } scale) parts.Add($"scale {scale.Current}%");
        if (d.IsPrimary) parts.Add("primary");
        if (d.HdrSupported) parts.Add($"HDR {(d.HdrEnabled ? "on" : "off")}");
        return string.Join(", ", parts);
    }
}
