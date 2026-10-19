using System.Drawing;
using MonitorSetup.Core.Models;

namespace MonitorSetup.App.UI;

/// <summary>What the main window shows and the user actions it reports. Implemented by <see cref="MainForm"/>.</summary>
internal interface IMainView
{
    event EventHandler? Loaded;
    event EventHandler? RefreshRequested;
    event EventHandler? IdentifyRequested;
    event EventHandler? DisplaysChangedExternally;
    event EventHandler<DisplayId>? DisplaySelected;
    event EventHandler? ToggleEnabledRequested;
    event EventHandler? MakePrimaryRequested;
    event EventHandler<IReadOnlyDictionary<DisplayId, Point>>? ApplyArrangementRequested;
    event EventHandler? ResetArrangementRequested;
    event EventHandler<DisplayMode>? ResolutionSelected;
    event EventHandler<int>? RefreshRateSelected;
    event EventHandler<DisplayOrientation>? OrientationSelected;
    event EventHandler<int>? ScaleSelected;
    event EventHandler<bool>? HdrToggled;
    event EventHandler<ProjectionMode>? ProjectionRequested;

    /// <summary>The user dragged displays but has not applied the new arrangement yet.</summary>
    bool HasUnappliedArrangement { get; }

    void ShowDisplays(IReadOnlyList<DisplayInfo> displays, DisplayId? selected);
    void ShowSelectedDisplay(SelectedDisplayModel model);
    void ShowStatus(string message, bool isError = false);
    void ShowError(string message);
    void SetBusy(bool busy);
}
