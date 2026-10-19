using System.Drawing;
using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using MonitorSetup.Infrastructure.Windows.Interop;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Services;

internal sealed class WindowsDisplayReader : IDisplayReader
{
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var (paths, modes) = DisplayConfig.Query(QDC_ALL_PATHS);

        // Windows lists one path per possible source for every target; keep the active one, else the first usable one.
        var chosen = new Dictionary<DisplayId, DISPLAYCONFIG_PATH_INFO>();
        foreach (var path in paths)
        {
            if (path.targetInfo.targetAvailable == 0)
                continue;
            var id = path.TargetId();
            if (!chosen.TryGetValue(id, out var existing) || (path.IsActive() && !existing.IsActive()))
                chosen[id] = path;
        }

        return chosen.Values
            .OrderBy(p => p.TargetId().AdapterId).ThenBy(p => p.targetInfo.id)
            .Select((p, i) => Describe(p, modes, i + 1))
            .ToList();
    }

    private static DisplayInfo Describe(DISPLAYCONFIG_PATH_INFO path, DISPLAYCONFIG_MODE_INFO[] modes, int number)
    {
        var target = DisplayConfig.TargetName(path.targetInfo.adapterId, path.targetInfo.id);
        var display = new DisplayInfo
        {
            Id = path.TargetId(),
            Number = number,
            FriendlyName = target.monitorFriendlyDeviceName ?? "",
            DevicePath = target.monitorDevicePath ?? "",
            Connection = OutputTechnology.Describe(path.targetInfo.outputTechnology),
        };
        if (!path.IsActive())
            return display;

        string gdiName = DisplayConfig.SourceGdiName(path.sourceInfo.adapterId, path.sourceInfo.id);
        var dm = Gdi.CurrentMode(gdiName);

        var bounds = new Rectangle(dm.dmPositionX, dm.dmPositionY, (int)dm.dmPelsWidth, (int)dm.dmPelsHeight);
        if (path.sourceInfo.modeInfoIdx < modes.Length && modes[path.sourceInfo.modeInfoIdx].infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
        {
            var sm = modes[path.sourceInfo.modeInfoIdx].sourceMode;
            bounds = new Rectangle(sm.position.x, sm.position.y, (int)sm.width, (int)sm.height);
        }

        var hdr = AdvancedColor.Get(path.targetInfo.adapterId, path.targetInfo.id);
        return display with
        {
            IsActive = true,
            IsPrimary = bounds.Location == Point.Empty,
            GdiName = gdiName,
            Bounds = bounds,
            RefreshRate = (int)dm.dmDisplayFrequency,
            BitsPerPixel = (int)dm.dmBitsPerPel,
            Orientation = (DisplayOrientation)dm.dmDisplayOrientation,
            Modes = ListModes(gdiName, dm.dmBitsPerPel),
            Scale = DpiScale.Get(path.sourceInfo.adapterId, path.sourceInfo.id),
            HdrSupported = hdr.Supported,
            HdrEnabled = hdr.Enabled,
        };
    }

    private static IReadOnlyList<DisplayMode> ListModes(string gdiName, uint bitsPerPixel) =>
        Gdi.AllModes(gdiName)
            .Where(m => m.dmBitsPerPel == bitsPerPixel)
            .GroupBy(m => (Width: (int)m.dmPelsWidth, Height: (int)m.dmPelsHeight))
            .OrderByDescending(g => g.Key.Width * g.Key.Height).ThenByDescending(g => g.Key.Width)
            .Select(g => new DisplayMode(g.Key.Width, g.Key.Height,
                g.Select(m => (int)m.dmDisplayFrequency).Distinct().OrderDescending().ToList()))
            .ToList();
}
