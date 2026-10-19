using System.Drawing;

namespace MonitorSetup.Core.Models;

public enum DisplayOrientation
{
    Landscape = 0,
    Portrait = 1,
    LandscapeFlipped = 2,
    PortraitFlipped = 3,
}

public enum ProjectionMode
{
    PcScreenOnly,
    Duplicate,
    Extend,
    SecondScreenOnly,
}

/// <summary>A resolution and the refresh rates available for it, highest first.</summary>
public sealed record DisplayMode(int Width, int Height, IReadOnlyList<int> RefreshRates)
{
    public override string ToString() => $"{Width} x {Height}";
}

/// <summary>Scale percentages a display supports, as offered by the Windows Settings app.</summary>
public sealed record DisplayScale(IReadOnlyList<int> Options, int Current, int Recommended);

public sealed record DisplayInfo
{
    public required DisplayId Id { get; init; }

    /// <summary>1-based number shown in the UI and by "Identify".</summary>
    public int Number { get; init; }

    public bool IsActive { get; init; }
    public bool IsPrimary { get; init; }

    public string FriendlyName { get; init; } = "";
    public string DevicePath { get; init; } = "";
    public string Connection { get; init; } = "";

    /// <summary>GDI name such as \\.\DISPLAY1; only set for active displays.</summary>
    public string? GdiName { get; init; }

    /// <summary>Desktop rectangle in physical pixels (active displays only).</summary>
    public Rectangle Bounds { get; init; }

    public int RefreshRate { get; init; }
    public int BitsPerPixel { get; init; }
    public DisplayOrientation Orientation { get; init; }

    /// <summary>Modes in the current orientation, largest first.</summary>
    public IReadOnlyList<DisplayMode> Modes { get; init; } = [];

    public DisplayScale? Scale { get; init; }

    public bool HdrSupported { get; init; }
    public bool HdrEnabled { get; init; }

    public string DisplayName => $"{Number}. {(string.IsNullOrWhiteSpace(FriendlyName) ? "Generic display" : FriendlyName)}";

    public override string ToString() => DisplayName;
}
