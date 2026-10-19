using System.Runtime.InteropServices;
using MonitorSetup.Core.Models;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Interop;

/// <summary>Managed wrappers around the CCD (Connecting and Configuring Displays) API.</summary>
internal static class DisplayConfig
{
    public static (DISPLAYCONFIG_PATH_INFO[] Paths, DISPLAYCONFIG_MODE_INFO[] Modes) Query(uint flags)
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
                continue; // the configuration changed between the two calls
            if (err != ERROR_SUCCESS)
                throw new DisplayException($"Could not read the display configuration (error {err}).");

            Array.Resize(ref paths, (int)pathCount);
            Array.Resize(ref modes, (int)modeCount);
            return (paths, modes);
        }
    }

    /// <summary>Applies exactly the supplied active paths and modes.</summary>
    public static void Apply(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes, string what)
    {
        int err = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes,
            SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE);
        if (err != ERROR_SUCCESS)
            throw new DisplayException($"Windows could not {what} (error {err}).");
    }

    public static bool IsActive(this DISPLAYCONFIG_PATH_INFO path) => (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0;

    public static DisplayId TargetId(this DISPLAYCONFIG_PATH_INFO path) =>
        new(path.targetInfo.adapterId.ToInt64(), path.targetInfo.id);

    /// <summary>The active path that drives a display, or throws if the display is off.</summary>
    public static DISPLAYCONFIG_PATH_INFO ActivePath(DisplayId id)
    {
        var (paths, _) = Query(QDC_ONLY_ACTIVE_PATHS);
        foreach (var path in paths)
            if (path.TargetId() == id)
                return path;
        throw new DisplayException("The display is turned off. Enable it first.");
    }

    public static string GdiName(DisplayId id)
    {
        var path = ActivePath(id);
        return SourceGdiName(path.sourceInfo.adapterId, path.sourceInfo.id);
    }

    public static string SourceGdiName(LUID adapter, uint sourceId)
    {
        var packet = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            header = Header<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME, adapter, sourceId),
        };
        int err = DisplayConfigGetDeviceInfo(ref packet);
        if (err != ERROR_SUCCESS)
            throw new DisplayException($"Could not read the display name (error {err}).");
        return packet.viewGdiDeviceName;
    }

    public static DISPLAYCONFIG_TARGET_DEVICE_NAME TargetName(LUID adapter, uint targetId)
    {
        var packet = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            header = Header<DISPLAYCONFIG_TARGET_DEVICE_NAME>(DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, adapter, targetId),
        };
        DisplayConfigGetDeviceInfo(ref packet);
        return packet;
    }

    public static DISPLAYCONFIG_DEVICE_INFO_HEADER Header<T>(int type, LUID adapter, uint id) where T : struct =>
        new() { type = type, size = (uint)Marshal.SizeOf<T>(), adapterId = adapter, id = id };
}
