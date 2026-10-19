using MonitorSetup.Core.Models;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Interop;

/// <summary>
/// Per-display scale through the undocumented DisplayConfig calls the Settings app uses.
/// Windows reports scale as steps relative to the recommended value.
/// </summary>
internal static class DpiScale
{
    private static readonly int[] Steps = [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];

    public static DisplayScale? Get(LUID adapter, uint sourceId)
    {
        var packet = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
        {
            header = DisplayConfig.Header<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>(DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE, adapter, sourceId),
        };
        if (DisplayConfigGetDeviceInfo(ref packet) != ERROR_SUCCESS)
            return null;

        int recommended = Math.Abs(packet.minScaleRel);
        if (recommended >= Steps.Length)
            return null;
        int max = Math.Min(recommended + packet.maxScaleRel, Steps.Length - 1);
        int current = Math.Clamp(recommended + packet.curScaleRel, 0, Steps.Length - 1);
        return new DisplayScale(Steps[..(max + 1)], Steps[current], Steps[recommended]);
    }

    public static void Set(LUID adapter, uint sourceId, int percent)
    {
        var scale = Get(adapter, sourceId) ?? throw new DisplayException("This display does not report scale settings.");
        int target = Array.IndexOf(Steps, percent);
        if (target < 0 || !scale.Options.Contains(percent))
            throw new DisplayException($"{percent}% is not a supported scale for this display.");

        var packet = new DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
        {
            header = DisplayConfig.Header<DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>(DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE, adapter, sourceId),
            scaleRel = target - Array.IndexOf(Steps, scale.Recommended),
        };
        int err = DisplayConfigSetDeviceInfo(ref packet);
        if (err != ERROR_SUCCESS)
            throw new DisplayException($"Windows could not change the scale (error {err}).");
    }
}
