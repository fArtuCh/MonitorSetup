using System.Drawing;
using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Abstractions;

/// <summary>Moves active displays on the virtual desktop in one atomic change.</summary>
public interface IDisplayPositioner
{
    /// <param name="positions">Final top-left corners; the primary display must be at (0,0).</param>
    /// <param name="newPrimary">Display that becomes the main display, if any.</param>
    void ApplyPositions(IReadOnlyDictionary<DisplayId, Point> positions, DisplayId? newPrimary = null);
}
