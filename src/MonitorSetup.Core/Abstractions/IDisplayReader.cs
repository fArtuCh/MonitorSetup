using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Abstractions;

/// <summary>Reads the displays that are connected right now, enabled or not.</summary>
public interface IDisplayReader
{
    IReadOnlyList<DisplayInfo> GetDisplays();
}
