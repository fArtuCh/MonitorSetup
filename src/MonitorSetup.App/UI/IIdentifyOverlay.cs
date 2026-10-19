using MonitorSetup.Core.Models;

namespace MonitorSetup.App.UI;

/// <summary>Briefly shows each display's number on the display itself.</summary>
internal interface IIdentifyOverlay
{
    void Show(IEnumerable<DisplayInfo> displays);
}

internal sealed class IdentifyOverlay : IIdentifyOverlay
{
    public void Show(IEnumerable<DisplayInfo> displays)
    {
        foreach (var d in displays.Where(d => d.IsActive))
            new IdentifyForm(d.Number, d.Bounds).Show();
    }
}
