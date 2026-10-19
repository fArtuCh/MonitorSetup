using MonitorSetup.Core.Abstractions;
using MonitorSetup.Infrastructure.Windows.Interop;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Services;

internal sealed class WindowsDisplaySnapshots : IDisplayConfigurationSnapshots
{
    private sealed record Snapshot(DISPLAYCONFIG_PATH_INFO[] Paths, DISPLAYCONFIG_MODE_INFO[] Modes) : IDisplaySnapshot;

    public IDisplaySnapshot Capture()
    {
        var (paths, modes) = DisplayConfig.Query(QDC_ONLY_ACTIVE_PATHS);
        return new Snapshot(paths, modes);
    }

    public void Restore(IDisplaySnapshot snapshot)
    {
        if (snapshot is not Snapshot s)
            throw new ArgumentException("The snapshot was not created by this service.", nameof(snapshot));
        DisplayConfig.Apply(s.Paths, s.Modes, "restore the previous settings");
    }
}
