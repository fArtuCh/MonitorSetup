using MonitorSetup.Core.Models;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Interop;

/// <summary>HDR ("advanced color") state of a display target.</summary>
internal static class AdvancedColor
{
    public static (bool Supported, bool Enabled) Get(LUID adapter, uint targetId)
    {
        var packet = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = DisplayConfig.Header<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO, adapter, targetId),
        };
        if (DisplayConfigGetDeviceInfo(ref packet) != ERROR_SUCCESS)
            return (false, false);
        return (packet.Supported && !packet.ForceDisabled, packet.Enabled);
    }

    public static void Set(LUID adapter, uint targetId, bool enabled)
    {
        var packet = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = DisplayConfig.Header<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE, adapter, targetId),
            value = enabled ? 1u : 0u,
        };
        int err = DisplayConfigSetDeviceInfo(ref packet);
        if (err != ERROR_SUCCESS)
            throw new DisplayException($"Windows could not change HDR (error {err}).");
    }
}
