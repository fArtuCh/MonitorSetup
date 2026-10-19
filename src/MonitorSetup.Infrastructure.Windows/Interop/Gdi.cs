using MonitorSetup.Core.Models;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Interop;

/// <summary>Managed wrappers around EnumDisplaySettingsEx / ChangeDisplaySettingsEx.</summary>
internal static class Gdi
{
    public static DEVMODE CurrentMode(string gdiName)
    {
        var dm = DEVMODE.Create();
        if (!EnumDisplaySettingsEx(gdiName, ENUM_CURRENT_SETTINGS, ref dm, 0))
            throw new DisplayException("Could not read the current display settings.");
        return dm;
    }

    public static IEnumerable<DEVMODE> AllModes(string gdiName)
    {
        var dm = DEVMODE.Create();
        for (int i = 0; EnumDisplaySettingsEx(gdiName, i, ref dm, 0); i++)
            yield return dm;
    }

    public static void Change(string gdiName, DEVMODE mode, uint flags, string what) =>
        Check(ChangeDisplaySettingsEx(gdiName, ref mode, IntPtr.Zero, flags, IntPtr.Zero), what);

    /// <summary>Applies changes previously staged with CDS_NORESET.</summary>
    public static void CommitStaged(string what) =>
        Check(ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero), what);

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
}
