using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Abstractions;

/// <summary>Turns HDR (advanced color) on or off for a display that supports it.</summary>
public interface IHdrSetter
{
    void SetHdr(DisplayId id, bool enabled);
}
