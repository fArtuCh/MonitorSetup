using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Abstractions;

/// <summary>Adds displays to or removes them from the desktop.</summary>
public interface IDisplayTopology
{
    void Activate(DisplayId id);
    void Deactivate(DisplayId id);
}
