using MonitorSetup.Display;
using MonitorSetup.Native;
using MonitorSetup.UI;

namespace MonitorSetup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
            return Cli.Run(args);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}

/// <summary>Small command line interface, handy for scripts and shortcuts.</summary>
internal static class Cli
{
    public static int Run(string[] args)
    {
        if (!Console.IsOutputRedirected)
            NativeMethods.AttachConsole(-1);

        try
        {
            var displays = DisplayManager.GetDisplays();
            switch (args[0].ToLowerInvariant())
            {
                case "--list":
                case "list":
                    foreach (var d in displays)
                    {
                        string state = d.IsActive
                            ? $"{d.Bounds.Width}x{d.Bounds.Height} @ {d.RefreshRate} Hz at ({d.Bounds.X},{d.Bounds.Y}), {d.Orientation}" +
                              (d.CurrentScale is int s ? $", scale {s}%" : "") +
                              (d.IsPrimary ? ", primary" : "") +
                              (d.HdrSupported ? $", HDR {(d.HdrEnabled ? "on" : "off")}" : "")
                            : "disabled";
                        Console.WriteLine($"{d.Number}. {d.FriendlyName} [{d.Connection}] {d.GdiName} - {state}");
                    }
                    return 0;

                case "enable":
                case "disable":
                    if (args.Length < 2 || !int.TryParse(args[1], out int n) || displays.FirstOrDefault(d => d.Number == n) is not { } display)
                    {
                        Console.Error.WriteLine("Usage: MonitorSetup (enable|disable) <number>   (see 'MonitorSetup list')");
                        return 2;
                    }
                    DisplayManager.SetEnabled(display, args[0].Equals("enable", StringComparison.OrdinalIgnoreCase));
                    Console.WriteLine($"Display {n} {args[0].ToLowerInvariant()}d.");
                    return 0;

                case "mode" when args.Length == 5 && Find(displays, args[1]) is { } d
                                 && int.TryParse(args[2], out int w) && int.TryParse(args[3], out int h) && int.TryParse(args[4], out int hz):
                    DisplayManager.SetMode(d, w, h, hz);
                    Console.WriteLine($"Display {d.Number} set to {w}x{h} @ {hz} Hz.");
                    return 0;

                case "move" when args.Length == 4 && Find(displays, args[1]) is { } d
                                 && int.TryParse(args[2], out int x) && int.TryParse(args[3], out int y):
                    var positions = displays.Where(o => o.IsActive).ToDictionary(o => o.Key, o => o.Bounds.Location);
                    positions[d.Key] = new Point(x, y);
                    DisplayManager.SetPositions(displays, positions);
                    Console.WriteLine($"Display {d.Number} moved to ({x},{y}).");
                    return 0;

                case "primary" when args.Length == 2 && Find(displays, args[1]) is { } d:
                    DisplayManager.SetPrimary(d, displays);
                    Console.WriteLine($"Display {d.Number} is now the main display.");
                    return 0;

                default:
                    Console.Error.WriteLine(
                        "Usage: MonitorSetup [list | enable <n> | disable <n> | mode <n> <width> <height> <hz> | move <n> <x> <y> | primary <n>]");
                    return 2;
            }
        }
        catch (DisplayException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static DisplayInfo? Find(IReadOnlyList<DisplayInfo> displays, string number) =>
        int.TryParse(number, out int n) ? displays.FirstOrDefault(d => d.Number == n) : null;
}
