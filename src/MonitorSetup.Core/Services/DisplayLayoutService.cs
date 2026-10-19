using System.Drawing;
using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Services;

public interface IDisplayLayoutService
{
    /// <summary>Moves displays; the whole layout is shifted so the main display stays at (0,0).</summary>
    void MoveDisplays(IReadOnlyDictionary<DisplayId, Point> positions);

    void MakePrimary(DisplayId id);
}

public sealed class DisplayLayoutService(IDisplayReader reader, IDisplayPositioner positioner) : IDisplayLayoutService
{
    public void MoveDisplays(IReadOnlyDictionary<DisplayId, Point> positions)
    {
        var primary = reader.GetDisplays().FirstOrDefault(d => d.IsPrimary && positions.ContainsKey(d.Id));
        var origin = primary is null ? Point.Empty : positions[primary.Id];
        positioner.ApplyPositions(Shift(positions, origin));
    }

    public void MakePrimary(DisplayId id)
    {
        var active = reader.GetDisplays().Where(d => d.IsActive).ToList();
        var target = active.FirstOrDefault(d => d.Id == id)
            ?? throw new DisplayException("Only an enabled display can be the main display.");

        var positions = active.ToDictionary(d => d.Id, d => d.Bounds.Location);
        positioner.ApplyPositions(Shift(positions, target.Bounds.Location), id);
    }

    internal static Dictionary<DisplayId, Point> Shift(IReadOnlyDictionary<DisplayId, Point> positions, Point origin) =>
        positions.ToDictionary(kv => kv.Key, kv => new Point(kv.Value.X - origin.X, kv.Value.Y - origin.Y));
}
