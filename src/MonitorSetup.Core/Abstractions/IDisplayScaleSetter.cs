using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Abstractions;

/// <summary>Changes the scale (100%, 125%, ...) of an active display.</summary>
public interface IDisplayScaleSetter
{
    void SetScale(DisplayId id, int percent);
}
