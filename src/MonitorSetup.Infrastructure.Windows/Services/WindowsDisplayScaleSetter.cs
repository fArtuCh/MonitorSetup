using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using MonitorSetup.Infrastructure.Windows.Interop;

namespace MonitorSetup.Infrastructure.Windows.Services;

internal sealed class WindowsDisplayScaleSetter : IDisplayScaleSetter
{
    public void SetScale(DisplayId id, int percent)
    {
        var path = DisplayConfig.ActivePath(id);
        DpiScale.Set(path.sourceInfo.adapterId, path.sourceInfo.id, percent);
    }
}