using System.Drawing;
using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Services;

public interface IDisplayPowerService
{
    void Enable(DisplayId id);
    void Disable(DisplayId id);
}

/// <summary>
/// Turns displays on and off. Remembers each display's mode and position when it is turned off
/// and puts it back the same way when it is turned on again.
/// </summary>
public sealed class DisplayPowerService(
    IDisplayReader reader,
    IDisplayTopology topology,
    IDisplayModeSetter modeSetter,
    IDisplayLayoutService layout,
    IDisplayStateStore store) : IDisplayPowerService
{
    public void Disable(DisplayId id)
    {
        var displays = reader.GetDisplays();
        var display = Find(displays, id);
        if (!display.IsActive)
            return;
        if (displays.Count(d => d.IsActive) == 1)
            throw new DisplayException("At least one display has to stay enabled.");

        store.Save(KeyOf(display), new SavedDisplayState(display.Bounds.X, display.Bounds.Y,
            display.Bounds.Width, display.Bounds.Height, display.RefreshRate, display.Orientation));
        topology.Deactivate(id);
    }

    public void Enable(DisplayId id)
    {
        var display = Find(reader.GetDisplays(), id);
        if (display.IsActive)
            return;

        topology.Activate(id);

        if (store.Get(KeyOf(display)) is { } saved)
            TryRestore(id, saved);
    }

    private void TryRestore(DisplayId id, SavedDisplayState saved)
    {
        try
        {
            var now = Find(reader.GetDisplays(), id);
            if (!now.IsActive)
                return;

            bool sameMode = now.Bounds.Width == saved.Width && now.Bounds.Height == saved.Height && now.RefreshRate == saved.RefreshRate;
            bool modeAvailable = now.Modes.Any(m => m.Width == saved.Width && m.Height == saved.Height && m.RefreshRates.Contains(saved.RefreshRate));
            if (!sameMode && modeAvailable && now.Orientation == saved.Orientation)
                modeSetter.SetMode(id, saved.Width, saved.Height, saved.RefreshRate);

            var displays = reader.GetDisplays();
            now = Find(displays, id);
            var wanted = new Rectangle(saved.X, saved.Y, now.Bounds.Width, now.Bounds.Height);
            bool overlaps = displays.Any(d => d.IsActive && d.Id != id && d.Bounds.IntersectsWith(wanted));
            if (overlaps || wanted.Location == now.Bounds.Location)
                return;

            var positions = displays.Where(d => d.IsActive).ToDictionary(d => d.Id, d => d.Bounds.Location);
            positions[id] = wanted.Location;
            layout.MoveDisplays(positions);
        }
        catch (DisplayException)
        {
            // The display is on; not getting its old layout back is not worth reporting as a failure.
        }
    }

    private static DisplayInfo Find(IReadOnlyList<DisplayInfo> displays, DisplayId id) =>
        displays.FirstOrDefault(d => d.Id == id) ?? throw new DisplayException("The display is no longer connected.");

    private static string KeyOf(DisplayInfo display) =>
        string.IsNullOrEmpty(display.DevicePath) ? display.Id.ToString() : display.DevicePath;
}
