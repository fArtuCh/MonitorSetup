using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using MonitorSetup.Infrastructure.Windows.Interop;

namespace MonitorSetup.Infrastructure.Windows.Services;

internal sealed class WindowsHdrSetter : IHdrSetter
{
    public void SetHdr(DisplayId id, bool enabled)
    {
        var path = DisplayConfig.ActivePath(id);
        AdvancedColor.Set(path.targetInfo.adapterId, path.targetInfo.id, enabled);
    }
}