using System.Drawing;
using System.Runtime.InteropServices;
using MonitorSetup.Native;
using static MonitorSetup.Native.NativeMethods;

namespace MonitorSetup.Display;

public sealed class DisplayException(string message) : Exception(message);

public enum ProjectionMode
{
    PcScreenOnly,
    Duplicate,
    Extend,
    SecondScreenOnly,
}

/// <summary>
/// Reads and changes the Windows display configuration using the CCD API
/// (QueryDisplayConfig / SetDisplayConfig) and the GDI API (ChangeDisplaySettingsEx).
/// </summary>
public static class DisplayManager
{
    // Scale steps used by the Settings app; the DPI scale API reports positions relative to the recommended one.
    private static readonly int[] ScaleSteps = [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];

    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var (paths, modes) = Query(QDC_ALL_PATHS);

        // Several paths exist per target (one per possible source); keep the active one, else the first usable one.
        var chosen = new Dictionary<(LUID, uint), DISPLAYCONFIG_PATH_INFO>();
        foreach (var path in paths)
        {
            if (path.targetInfo.targetAvailable == 0)
                continue;
            var key = (path.targetInfo.adapterId, path.targetInfo.id);
            bool active = (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0;
            if (!chosen.TryGetValue(key, out var existing) || (active && (existing.flags & DISPLAYCONFIG_PATH_ACTIVE) == 0))
                chosen[key] = path;
        }

        var result = chosen.Values
            .Select(p => Describe(p, modes))
            .OrderBy(d => d.AdapterId.HighPart).ThenBy(d => d.AdapterId.LowPart).ThenBy(d => d.TargetId)
            .ToList();

        for (int i = 0; i < result.Count; i++)
            result[i].Number = i + 1;
        return result;
    }

    private static DisplayInfo Describe(DISPLAYCONFIG_PATH_INFO path, DISPLAYCONFIG_MODE_INFO[] modes)
    {
        bool active = (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0;
        var target = GetTargetName(path.targetInfo.adapterId, path.targetInfo.id);

        if (!active)
        {
            return new DisplayInfo
            {
                AdapterId = path.targetInfo.adapterId,
                TargetId = path.targetInfo.id,
                SourceId = path.sourceInfo.id,
                IsActive = false,
                IsAvailable = true,
                FriendlyName = target.monitorFriendlyDeviceName ?? "",
                DevicePath = target.monitorDevicePath ?? "",
                Connection = DescribeConnection(path.targetInfo.outputTechnology),
            };
        }

        string gdiName = GetSourceGdiName(path.sourceInfo.adapterId, path.sourceInfo.id);
        var dm = DEVMODE.Create();
        EnumDisplaySettingsEx(gdiName, ENUM_CURRENT_SETTINGS, ref dm, 0);

        var bounds = new Rectangle(dm.dmPositionX, dm.dmPositionY, (int)dm.dmPelsWidth, (int)dm.dmPelsHeight);
        if (path.sourceInfo.modeInfoIdx < modes.Length && modes[path.sourceInfo.modeInfoIdx].infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
        {
            var sm = modes[path.sourceInfo.modeInfoIdx].sourceMode;
            bounds = new Rectangle(sm.position.x, sm.position.y, (int)sm.width, (int)sm.height);
        }

        var scale = GetScale(path.sourceInfo.adapterId, path.sourceInfo.id);
        var hdr = GetAdvancedColor(path.targetInfo.adapterId, path.targetInfo.id);

        return new DisplayInfo
        {
            AdapterId = path.targetInfo.adapterId,
            TargetId = path.targetInfo.id,
            SourceId = path.sourceInfo.id,
            IsActive = true,
            IsAvailable = true,
            IsPrimary = bounds.X == 0 && bounds.Y == 0,
            FriendlyName = target.monitorFriendlyDeviceName ?? "",
            DevicePath = target.monitorDevicePath ?? "",
            Connection = DescribeConnection(path.targetInfo.outputTechnology),
            GdiName = gdiName,
            Bounds = bounds,
            RefreshRate = (int)dm.dmDisplayFrequency,
            BitsPerPixel = (int)dm.dmBitsPerPel,
            Orientation = (DisplayOrientation)dm.dmDisplayOrientation,
            Modes = GetModes(gdiName, (int)dm.dmBitsPerPel),
            ScaleOptions = scale.options,
            CurrentScale = scale.current,
            RecommendedScale = scale.recommended,
            HdrSupported = hdr is { Supported: true, ForceDisabled: false },
            HdrEnabled = hdr?.Enabled ?? false,
        };
    }

    #region Enable / disable / projection

    public static void SetEnabled(DisplayInfo display, bool enabled)
    {
        if (!enabled)
        {
            if (display.IsActive)
                SavedDisplayState.Save(display);
            Disable(display);
            return;
        }

        Enable(display);
        RestoreSavedState(display);
    }

    /// <summary>Puts a re-enabled display back to the mode and position it had when it was disabled.</summary>
    private static void RestoreSavedState(DisplayInfo display)
    {
        if (SavedDisplayState.Get(display) is not { } saved)
            return;

        var all = GetDisplays();
        var now = all.FirstOrDefault(d => d.Key == display.Key && d.IsActive);
        if (now is null)
            return;

        try
        {
            if (now.Orientation == saved.Orientation && now.Modes.Any(m => m.Width == saved.Width && m.Height == saved.Height && m.RefreshRates.Contains(saved.RefreshRate))
                && (now.Bounds.Width != saved.Width || now.Bounds.Height != saved.Height || now.RefreshRate != saved.RefreshRate))
            {
                SetMode(now, saved.Width, saved.Height, saved.RefreshRate);
                all = GetDisplays();
                now = all.First(d => d.Key == display.Key);
            }

            var wanted = new Rectangle(saved.X, saved.Y, now.Bounds.Width, now.Bounds.Height);
            bool overlaps = all.Any(d => d.IsActive && d.Key != now.Key && d.Bounds.IntersectsWith(wanted));
            if (!overlaps && wanted.Location != now.Bounds.Location)
            {
                var positions = all.Where(d => d.IsActive).ToDictionary(d => d.Key, d => d.Bounds.Location);
                positions[now.Key] = wanted.Location;
                SetPositions(all, positions);
            }
        }
        catch (DisplayException)
        {
            // The display is on; failing to restore its old layout is not an error worth reporting.
        }
    }

    private static void Disable(DisplayInfo display)
    {
        var (paths, modes) = Query(QDC_ALL_PATHS);
        var active = paths.Where(IsActive).ToList();
        var remaining = active.Where(p => !IsTarget(p, display)).ToArray();

        if (remaining.Length == active.Count)
            return; // already disabled
        if (remaining.Length == 0)
            throw new DisplayException("At least one display has to stay enabled.");

        Apply(remaining, modes, "disable the display");
    }

    private static void Enable(DisplayInfo display)
    {
        var (paths, modes) = Query(QDC_ALL_PATHS);
        var active = paths.Where(IsActive).ToList();
        if (active.Any(p => IsTarget(p, display)))
            return; // already enabled

        // Pick a path to this target whose source is not already driving another display.
        var usedSources = active.Select(p => (p.sourceInfo.adapterId, p.sourceInfo.id)).ToHashSet();
        var candidates = paths
            .Where(p => IsTarget(p, display) && p.targetInfo.targetAvailable != 0)
            .Where(p => !usedSources.Contains((p.sourceInfo.adapterId, p.sourceInfo.id)))
            .ToList();
        if (candidates.Count == 0)
            throw new DisplayException("No free graphics output is available to drive this display.");

        int lastError = 0;
        foreach (var candidate in candidates)
        {
            var path = candidate;
            path.flags |= DISPLAYCONFIG_PATH_ACTIVE;
            path.sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
            path.targetInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
            var newPaths = active.Append(path).ToArray();

            // Prefer the layout Windows remembers for this set of displays (position, refresh rate, ...).
            lastError = SetDisplayConfig((uint)newPaths.Length, newPaths, 0, null,
                SDC_APPLY | SDC_TOPOLOGY_SUPPLIED | SDC_ALLOW_PATH_ORDER_CHANGES);
            if (lastError == ERROR_SUCCESS)
                return;

            // Otherwise keep the current displays as they are and let Windows pick a mode for the new one.
            lastError = SetDisplayConfig((uint)newPaths.Length, newPaths, (uint)modes.Length, modes,
                SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE);
            if (lastError == ERROR_SUCCESS)
                return;
        }

        throw new DisplayException($"Windows refused to enable the display (error {lastError}).");
    }

    private const uint SDC_ALLOW_PATH_ORDER_CHANGES = 0x2000;

    public static void SetProjection(ProjectionMode mode)
    {
        uint topology = mode switch
        {
            ProjectionMode.PcScreenOnly => SDC_TOPOLOGY_INTERNAL,
            ProjectionMode.Duplicate => SDC_TOPOLOGY_CLONE,
            ProjectionMode.Extend => SDC_TOPOLOGY_EXTEND,
            ProjectionMode.SecondScreenOnly => SDC_TOPOLOGY_EXTERNAL,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        int err = SetDisplayConfig(0, null, 0, null, SDC_APPLY | topology);
        if (err != ERROR_SUCCESS)
            throw new DisplayException($"Windows could not switch the projection mode (error {err}).");
    }

    #endregion

    #region Snapshots (used to revert changes)

    public sealed class Snapshot
    {
        internal DISPLAYCONFIG_PATH_INFO[] Paths { get; init; } = [];
        internal DISPLAYCONFIG_MODE_INFO[] Modes { get; init; } = [];
    }

    public static Snapshot TakeSnapshot()
    {
        var (paths, modes) = Query(QDC_ONLY_ACTIVE_PATHS);
        return new Snapshot { Paths = paths, Modes = modes };
    }

    public static void Restore(Snapshot snapshot)
    {
        Apply(snapshot.Paths, snapshot.Modes, "restore the previous settings");
    }

    #endregion

    #region Resolution, refresh rate, orientation, position

    public static void SetMode(DisplayInfo display, int width, int height, int refreshRate)
    {
        var dm = CurrentMode(display);
        dm.dmPelsWidth = (uint)width;
        dm.dmPelsHeight = (uint)height;
        dm.dmDisplayFrequency = (uint)refreshRate;
        dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
        Check(ChangeDisplaySettingsEx(display.GdiName, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero), "change the resolution");
    }

    public static void SetOrientation(DisplayInfo display, DisplayOrientation orientation)
    {
        var dm = CurrentMode(display);
        bool wasPortrait = dm.dmDisplayOrientation % 2 == 1;
        bool isPortrait = (int)orientation % 2 == 1;
        if (wasPortrait != isPortrait)
            (dm.dmPelsWidth, dm.dmPelsHeight) = (dm.dmPelsHeight, dm.dmPelsWidth);
        dm.dmDisplayOrientation = (uint)orientation;
        dm.dmFields = DM_DISPLAYORIENTATION | DM_PELSWIDTH | DM_PELSHEIGHT;
        Check(ChangeDisplaySettingsEx(display.GdiName, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero), "change the orientation");
    }

    public static void SetPrimary(DisplayInfo display, IReadOnlyList<DisplayInfo> all)
    {
        var offset = display.Bounds.Location;
        var positions = all.Where(d => d.IsActive)
            .ToDictionary(d => d.Key, d => new Point(d.Bounds.X - offset.X, d.Bounds.Y - offset.Y));
        ApplyPositions(all, positions, display);
    }

    /// <summary>Moves displays to new desktop positions. The primary display always stays at (0,0).</summary>
    public static void SetPositions(IReadOnlyList<DisplayInfo> all, IReadOnlyDictionary<string, Point> positions)
    {
        var primary = all.FirstOrDefault(d => d.IsPrimary && positions.ContainsKey(d.Key));
        var offset = primary is null ? Point.Empty : positions[primary.Key];
        var normalized = positions.ToDictionary(kv => kv.Key, kv => new Point(kv.Value.X - offset.X, kv.Value.Y - offset.Y));
        ApplyPositions(all, normalized, null);
    }

    private static void ApplyPositions(IReadOnlyList<DisplayInfo> all, IReadOnlyDictionary<string, Point> positions, DisplayInfo? newPrimary)
    {
        foreach (var d in all.Where(d => d.IsActive && positions.ContainsKey(d.Key)))
        {
            var dm = CurrentMode(d);
            var p = positions[d.Key];
            dm.dmPositionX = p.X;
            dm.dmPositionY = p.Y;
            dm.dmFields = DM_POSITION;
            uint flags = CDS_UPDATEREGISTRY | CDS_NORESET;
            if (newPrimary is not null && d.Key == newPrimary.Key)
                flags |= CDS_SET_PRIMARY;
            Check(ChangeDisplaySettingsEx(d.GdiName, ref dm, IntPtr.Zero, flags, IntPtr.Zero), "move the display");
        }
        Check(ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero), "apply the arrangement");
    }

    private static DEVMODE CurrentMode(DisplayInfo display)
    {
        if (display.GdiName is null)
            throw new DisplayException("The display is turned off. Enable it first.");
        var dm = DEVMODE.Create();
        if (!EnumDisplaySettingsEx(display.GdiName, ENUM_CURRENT_SETTINGS, ref dm, 0))
            throw new DisplayException("Could not read the current display settings.");
        return dm;
    }

    private static IReadOnlyList<DisplayModeOption> GetModes(string gdiName, int bitsPerPixel)
    {
        var rates = new Dictionary<(int w, int h), SortedSet<int>>();
        var dm = DEVMODE.Create();
        for (int i = 0; EnumDisplaySettingsEx(gdiName, i, ref dm, 0); i++)
        {
            if (dm.dmBitsPerPel != bitsPerPixel)
                continue;
            var key = ((int)dm.dmPelsWidth, (int)dm.dmPelsHeight);
            if (!rates.TryGetValue(key, out var set))
                rates[key] = set = [];
            set.Add((int)dm.dmDisplayFrequency);
        }
        return rates
            .OrderByDescending(kv => kv.Key.w * kv.Key.h).ThenByDescending(kv => kv.Key.w)
            .Select(kv => new DisplayModeOption(kv.Key.w, kv.Key.h, kv.Value.Reverse().ToList()))
            .ToList();
    }

    #endregion

    #region Scale and HDR

    public static void SetScale(DisplayInfo display, int percent)
    {
        if (display.RecommendedScale is not int recommended)
            throw new DisplayException("This display does not report scale settings.");
        int target = Array.IndexOf(ScaleSteps, percent);
        int rec = Array.IndexOf(ScaleSteps, recommended);
        if (target < 0 || rec < 0)
            throw new DisplayException($"{percent}% is not a supported scale.");

        var packet = new DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE, Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>(), display.AdapterId, display.SourceId),
            scaleRel = target - rec,
        };
        int err = DisplayConfigSetDeviceInfo(ref packet);
        if (err != ERROR_SUCCESS)
            throw new DisplayException($"Windows could not change the scale (error {err}).");
    }

    public static void SetHdr(DisplayInfo display, bool enabled)
    {
        var packet = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE, Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(), display.AdapterId, display.TargetId),
            value = enabled ? 1u : 0u,
        };
        int err = DisplayConfigSetDeviceInfo(ref packet);
        if (err != ERROR_SUCCESS)
            throw new DisplayException($"Windows could not change HDR (error {err}).");
    }

    private static (IReadOnlyList<int> options, int? current, int? recommended) GetScale(LUID adapter, uint sourceId)
    {
        var packet = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE, Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>(), adapter, sourceId),
        };
        if (DisplayConfigGetDeviceInfo(ref packet) != ERROR_SUCCESS)
            return (Array.Empty<int>(), null, null);

        int rec = Math.Abs(packet.minScaleRel);
        int max = Math.Min(rec + packet.maxScaleRel, ScaleSteps.Length - 1);
        int cur = Math.Clamp(rec + packet.curScaleRel, 0, ScaleSteps.Length - 1);
        if (rec >= ScaleSteps.Length)
            return (Array.Empty<int>(), null, null);
        return (ScaleSteps[..(max + 1)], ScaleSteps[cur], ScaleSteps[rec]);
    }

    private static DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO? GetAdvancedColor(LUID adapter, uint targetId)
    {
        var packet = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO, Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(), adapter, targetId),
        };
        return DisplayConfigGetDeviceInfo(ref packet) == ERROR_SUCCESS ? packet : null;
    }

    #endregion

    #region Helpers

    private static (DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes) Query(uint flags)
    {
        while (true)
        {
            int err = GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount);
            if (err != ERROR_SUCCESS)
                throw new DisplayException($"Could not read the display configuration (error {err}).");

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            err = QueryDisplayConfig(flags, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (err == ERROR_INSUFFICIENT_BUFFER)
                continue; // configuration changed between the two calls
            if (err != ERROR_SUCCESS)
                throw new DisplayException($"Could not read the display configuration (error {err}).");

            Array.Resize(ref paths, (int)pathCount);
            Array.Resize(ref modes, (int)modeCount);
            return (paths, modes);
        }
    }

    private static void Apply(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes, string what)
    {
        int err = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes,
            SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE);
        if (err != ERROR_SUCCESS)
            throw new DisplayException($"Windows could not {what} (error {err}).");
    }

    private static bool IsActive(DISPLAYCONFIG_PATH_INFO p) => (p.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0;

    private static bool IsTarget(DISPLAYCONFIG_PATH_INFO p, DisplayInfo d) =>
        p.targetInfo.adapterId.Equals(d.AdapterId) && p.targetInfo.id == d.TargetId;

    private static DISPLAYCONFIG_DEVICE_INFO_HEADER Header(int type, int size, LUID adapter, uint id) =>
        new() { type = type, size = (uint)size, adapterId = adapter, id = id };

    private static DISPLAYCONFIG_TARGET_DEVICE_NAME GetTargetName(LUID adapter, uint targetId)
    {
        var packet = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(), adapter, targetId),
        };
        DisplayConfigGetDeviceInfo(ref packet);
        return packet;
    }

    private static string GetSourceGdiName(LUID adapter, uint sourceId)
    {
        var packet = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME, Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(), adapter, sourceId),
        };
        int err = DisplayConfigGetDeviceInfo(ref packet);
        if (err != ERROR_SUCCESS)
            throw new DisplayException($"Could not read the display name (error {err}).");
        return packet.viewGdiDeviceName;
    }

    private static string DescribeConnection(uint tech) => tech switch
    {
        0 => "VGA",
        1 => "S-Video",
        2 => "Composite",
        3 => "Component",
        4 => "DVI",
        5 => "HDMI",
        6 => "LVDS (built-in)",
        8 => "D-Jpn",
        9 => "SDI",
        10 => "DisplayPort",
        11 => "DisplayPort (built-in)",
        12 => "UDI",
        13 => "UDI (built-in)",
        14 => "SDTV dongle",
        15 => "Miracast (wireless)",
        16 => "Indirect (wired)",
        17 => "Indirect (virtual)",
        18 => "DisplayPort over USB",
        0x80000000 => "Built-in",
        _ => "Other",
    };

    private static void Check(int result, string what)
    {
        if (result == DISP_CHANGE_SUCCESSFUL)
            return;
        string reason = result switch
        {
            1 => "a restart is required",
            -1 => "the driver rejected the mode",
            -2 => "the mode is not supported",
            -3 => "the settings could not be saved",
            -4 => "invalid flags",
            -5 => "invalid parameters",
            -6 => "the display is not part of the desktop",
            _ => $"error {result}",
        };
        throw new DisplayException($"Windows could not {what}: {reason}.");
    }

    #endregion
}
