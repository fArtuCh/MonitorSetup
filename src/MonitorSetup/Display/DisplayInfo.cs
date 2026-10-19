using System.Drawing;
using MonitorSetup.Native;

namespace MonitorSetup.Display;

public enum DisplayOrientation
{
    Landscape = 0,
    Portrait = 1,
    LandscapeFlipped = 2,
    PortraitFlipped = 3,
}

public sealed record DisplayModeOption(int Width, int Height, IReadOnlyList<int> RefreshRates)
{
    public override string ToString() => $"{Width} x {Height}";
}

public sealed class DisplayInfo
{
    internal LUID AdapterId { get; init; }
    public uint TargetId { get; init; }
    internal uint SourceId { get; init; }

    /// <summary>1-based number shown in the UI and by "Identify".</summary>
    public int Number { get; set; }

    public bool IsActive { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsPrimary { get; init; }

    public string FriendlyName { get; init; } = "";
    public string DevicePath { get; init; } = "";
    public string Connection { get; init; } = "";

    /// <summary>GDI name such as \\.\DISPLAY1, only set for active displays.</summary>
    public string? GdiName { get; init; }

    /// <summary>Desktop rectangle in physical pixels (active displays only).</summary>
    public Rectangle Bounds { get; init; }

    public int RefreshRate { get; init; }
    public int BitsPerPixel { get; init; }
    public DisplayOrientation Orientation { get; init; }

    /// <summary>Modes in the current orientation, largest first.</summary>
    public IReadOnlyList<DisplayModeOption> Modes { get; init; } = Array.Empty<DisplayModeOption>();

    /// <summary>Scale percentages the display supports (e.g. 100, 125, 150).</summary>
    public IReadOnlyList<int> ScaleOptions { get; init; } = Array.Empty<int>();
    public int? CurrentScale { get; init; }
    public int? RecommendedScale { get; init; }

    public bool HdrSupported { get; init; }
    public bool HdrEnabled { get; init; }

    public string Key => $"{AdapterId}:{TargetId}";

    public string DisplayName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(FriendlyName) ? "Generic display" : FriendlyName;
            return $"{Number}. {name}";
        }
    }

    public override string ToString() => DisplayName;
}
