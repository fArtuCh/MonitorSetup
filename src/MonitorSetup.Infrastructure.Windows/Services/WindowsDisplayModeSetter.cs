using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using MonitorSetup.Infrastructure.Windows.Interop;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Services;

internal sealed class WindowsDisplayModeSetter : IDisplayModeSetter
{
    public void SetMode(DisplayId id, int width, int height, int refreshRate)
    {
        string gdiName = DisplayConfig.GdiName(id);
        var dm = Gdi.CurrentMode(gdiName);
        dm.dmPelsWidth = (uint)width;
        dm.dmPelsHeight = (uint)height;
        dm.dmDisplayFrequency = (uint)refreshRate;
        dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
        Gdi.Change(gdiName, dm, CDS_UPDATEREGISTRY, "change the resolution");
    }

    public void SetOrientation(DisplayId id, DisplayOrientation orientation)
    {
        string gdiName = DisplayConfig.GdiName(id);
        var dm = Gdi.CurrentMode(gdiName);
        bool wasPortrait = dm.dmDisplayOrientation % 2 == 1;
        bool isPortrait = (int)orientation % 2 == 1;
        if (wasPortrait != isPortrait)
            (dm.dmPelsWidth, dm.dmPelsHeight) = (dm.dmPelsHeight, dm.dmPelsWidth);
        dm.dmDisplayOrientation = (uint)orientation;
        dm.dmFields = DM_DISPLAYORIENTATION | DM_PELSWIDTH | DM_PELSHEIGHT;
        Gdi.Change(gdiName, dm, CDS_UPDATEREGISTRY, "change the orientation");
    }
}
