using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Abstractions;

/// <summary>Switches between extend, duplicate and single-screen modes (what Win+P does).</summary>
public interface IProjectionSwitcher
{
    void SetProjection(ProjectionMode mode);
}
