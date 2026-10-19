using System.Drawing;
using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using MonitorSetup.Infrastructure.Windows.Interop;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Services;

internal sealed class WindowsDisplayPositioner : IDisplayPositioner
{
    public void ApplyPositions(IReadOnlyDictionary<DisplayId, Point> positions, DisplayId? newPrimary = null)
    {
        // Stage every move, then commit them together so Windows never sees overlapping displays.
        foreach (var (id, position) in positions)
        {
            string gdiName = DisplayConfig.GdiName(id);
            var dm = Gdi.CurrentMode(gdiName);
            dm.dmPositionX = position.X;
            dm.dmPositionY = position.Y;
            dm.dmFields = DM_POSITION;

            uint flags = CDS_UPDATEREGISTRY | CDS_NORESET;
            if (id == newPrimary)
                flags |= CDS_SET_PRIMARY;
            Gdi.Change(gdiName, dm, flags, "move the display");
        }
        Gdi.CommitStaged("apply the arrangement");
    }
}
