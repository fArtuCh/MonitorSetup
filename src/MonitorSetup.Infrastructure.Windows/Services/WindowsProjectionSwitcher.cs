using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Services;

internal sealed class WindowsProjectionSwitcher : IProjectionSwitcher
{
    public void SetProjection(ProjectionMode mode)
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
}
