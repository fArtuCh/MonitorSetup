using MonitorSetup.Core.Models;

namespace MonitorSetup.App.UI;

/// <summary>Everything the settings panel needs to show for the selected display.</summary>
internal sealed record SelectedDisplayModel
{
    public static SelectedDisplayModel Empty { get; } = new();

    public DisplayId? Id { get; init; }
    public string StateText { get; init; } = "No display";
    public string ToggleText { get; init; } = "Enable this display";
    public bool CanToggle { get; init; }

    public bool IsPrimary { get; init; }
    public bool CanMakePrimary { get; init; }

    public bool CanChangeMode { get; init; }
    public IReadOnlyList<DisplayMode> Modes { get; init; } = [];
    public DisplayMode? CurrentMode { get; init; }
    public int? CurrentRefreshRate { get; init; }
    public DisplayOrientation Orientation { get; init; }

    public IReadOnlyList<int> ScaleOptions { get; init; } = [];
    public int? CurrentScale { get; init; }
    public int? RecommendedScale { get; init; }

    public bool HdrSupported { get; init; }
    public bool HdrEnabled { get; init; }

    public string Details { get; init; } = "";

    public static SelectedDisplayModel From(DisplayInfo? display, int activeCount)
    {
        if (display is null)
            return Empty;

        bool active = display.IsActive;
        return new SelectedDisplayModel
        {
            Id = display.Id,
            StateText = active ? "Enabled" : "Disabled",
            ToggleText = active ? "Disable this display" : "Enable this display",
            CanToggle = !active || activeCount > 1,
            IsPrimary = display.IsPrimary,
            CanMakePrimary = active && activeCount > 1 && !display.IsPrimary,
            CanChangeMode = active,
            Modes = display.Modes,
            CurrentMode = display.Modes.FirstOrDefault(m => m.Width == display.Bounds.Width && m.Height == display.Bounds.Height),
            CurrentRefreshRate = active ? display.RefreshRate : null,
            Orientation = display.Orientation,
            ScaleOptions = display.Scale?.Options ?? [],
            CurrentScale = display.Scale?.Current,
            RecommendedScale = display.Scale?.Recommended,
            HdrSupported = display.HdrSupported,
            HdrEnabled = display.HdrEnabled,
            Details = active
                ? $"{display.Connection} · {display.GdiName} · position ({display.Bounds.X}, {display.Bounds.Y}) · {display.BitsPerPixel}-bit"
                : $"{display.Connection} · connected but not part of the desktop",
        };
    }
}
