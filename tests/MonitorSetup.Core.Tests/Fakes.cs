using System.Drawing;
using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Tests;

/// <summary>In-memory display system: the fakes below read and change this list.</summary>
internal sealed class FakeDisplays : IDisplayReader, IDisplayTopology, IDisplayModeSetter, IDisplayPositioner
{
    public List<DisplayInfo> Displays { get; } = [];
    public List<string> Calls { get; } = [];

    public static DisplayInfo Display(uint target, Rectangle bounds, bool active = true, int hz = 60) => new()
    {
        Id = new DisplayId(1, target),
        Number = (int)target,
        DevicePath = $@"\\?\DISPLAY#{target}",
        IsActive = active,
        IsPrimary = active && bounds.Location == Point.Empty,
        Bounds = active ? bounds : Rectangle.Empty,
        RefreshRate = active ? hz : 0,
        Modes = [new DisplayMode(1920, 1080, [60, 59]), new DisplayMode(1280, 720, [60])],
    };

    public IReadOnlyList<DisplayInfo> GetDisplays() => Displays.ToList();

    public void Activate(DisplayId id)
    {
        Calls.Add($"activate {id.TargetId}");
        // Windows picks a default spot and rate for a re-enabled display.
        Replace(id, d => d with { IsActive = true, Bounds = new Rectangle(10_000, 0, 1920, 1080), RefreshRate = 59 });
    }

    public void Deactivate(DisplayId id)
    {
        Calls.Add($"deactivate {id.TargetId}");
        Replace(id, d => d with { IsActive = false, IsPrimary = false, Bounds = Rectangle.Empty });
    }

    public void SetMode(DisplayId id, int width, int height, int refreshRate)
    {
        Calls.Add($"mode {id.TargetId} {width}x{height}@{refreshRate}");
        Replace(id, d => d with { Bounds = d.Bounds with { Width = width, Height = height }, RefreshRate = refreshRate });
    }

    public void SetOrientation(DisplayId id, DisplayOrientation orientation) => Calls.Add($"rotate {id.TargetId} {orientation}");

    public Dictionary<DisplayId, Point>? LastPositions { get; private set; }
    public DisplayId? LastNewPrimary { get; private set; }

    public void ApplyPositions(IReadOnlyDictionary<DisplayId, Point> positions, DisplayId? newPrimary = null)
    {
        Calls.Add("positions");
        LastPositions = positions.ToDictionary();
        LastNewPrimary = newPrimary;
        foreach (var (id, p) in positions)
            Replace(id, d => d with { Bounds = d.Bounds with { Location = p }, IsPrimary = p == Point.Empty });
    }

    private void Replace(DisplayId id, Func<DisplayInfo, DisplayInfo> change)
    {
        int i = Displays.FindIndex(d => d.Id == id);
        Displays[i] = change(Displays[i]);
    }
}

internal sealed class InMemoryStateStore : IDisplayStateStore
{
    public Dictionary<string, SavedDisplayState> States { get; } = [];
    public void Save(string displayKey, SavedDisplayState state) => States[displayKey] = state;
    public SavedDisplayState? Get(string displayKey) => States.GetValueOrDefault(displayKey);
}
