using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Abstractions;

/// <summary>Changes resolution, refresh rate and orientation of an active display.</summary>
public interface IDisplayModeSetter
{
    void SetMode(DisplayId id, int width, int height, int refreshRate);
    void SetOrientation(DisplayId id, DisplayOrientation orientation);
}
