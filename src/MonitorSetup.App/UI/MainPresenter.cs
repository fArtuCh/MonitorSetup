using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using MonitorSetup.Core.Services;

namespace MonitorSetup.App.UI;

/// <summary>
/// Drives <see cref="IMainView"/>: reads displays, turns user actions into display changes,
/// and offers to revert changes the user does not keep.
/// </summary>
internal sealed class MainPresenter(
    IMainView view,
    IDisplayReader reader,
    IDisplayPowerService power,
    IDisplayLayoutService layout,
    IDisplayModeSetter modes,
    IDisplayScaleSetter scale,
    IHdrSetter hdr,
    IProjectionSwitcher projection,
    IDisplayConfigurationSnapshots snapshots,
    IChangeConfirmation confirmation,
    IIdentifyOverlay identify)
{
    private IReadOnlyList<DisplayInfo> _displays = [];
    private DisplayId? _selected;
    private bool _busy;

    private DisplayInfo? Selected => _displays.FirstOrDefault(d => d.Id == _selected);

    public void Start()
    {
        view.Loaded += (_, _) => Reload();
        view.RefreshRequested += (_, _) => Reload();
        view.ResetArrangementRequested += (_, _) => Reload();
        view.DisplaysChangedExternally += (_, _) =>
        {
            if (!_busy && !view.HasUnappliedArrangement)
                Reload();
        };
        view.IdentifyRequested += (_, _) => identify.Show(_displays);
        view.DisplaySelected += (_, id) => Select(id);

        view.ToggleEnabledRequested += (_, _) => WithSelected(d =>
        {
            if (d.IsActive)
                Change($"{d.DisplayName} disabled.", confirm: true, () => power.Disable(d.Id));
            else
                Change($"{d.DisplayName} enabled.", confirm: true, () => power.Enable(d.Id));
        });

        view.MakePrimaryRequested += (_, _) => WithSelected(d =>
            Change($"{d.DisplayName} is now the main display.", confirm: true, () => layout.MakePrimary(d.Id)));

        view.ApplyArrangementRequested += (_, positions) =>
            Change("Arrangement applied.", confirm: true, () => layout.MoveDisplays(positions));

        view.ResolutionSelected += (_, mode) => WithSelected(d =>
        {
            int rate = mode.RefreshRates.Contains(d.RefreshRate) ? d.RefreshRate : mode.RefreshRates[0];
            Change($"Resolution set to {mode}.", confirm: true, () => modes.SetMode(d.Id, mode.Width, mode.Height, rate));
        });

        view.RefreshRateSelected += (_, hz) => WithSelected(d =>
            Change($"Refresh rate set to {hz} Hz.", confirm: true, () => modes.SetMode(d.Id, d.Bounds.Width, d.Bounds.Height, hz)));

        view.OrientationSelected += (_, orientation) => WithSelected(d =>
            Change($"Orientation set to {orientation}.", confirm: true, () => modes.SetOrientation(d.Id, orientation)));

        view.ScaleSelected += (_, percent) => WithSelected(d =>
            Change($"Scale set to {percent}%. Some apps update after you sign out.", confirm: false, () => scale.SetScale(d.Id, percent)));

        view.HdrToggled += (_, on) => WithSelected(d =>
            Change(on ? "HDR turned on." : "HDR turned off.", confirm: false, () => hdr.SetHdr(d.Id, on)));

        view.ProjectionRequested += (_, mode) =>
            Change("Projection mode changed.", confirm: true, () => projection.SetProjection(mode));
    }

    private void Reload()
    {
        try
        {
            _displays = reader.GetDisplays();
        }
        catch (DisplayException ex)
        {
            _displays = [];
            view.ShowStatus(ex.Message, isError: true);
        }

        if (Selected is null)
            _selected = (_displays.FirstOrDefault(d => d.IsPrimary) ?? _displays.FirstOrDefault())?.Id;

        view.ShowDisplays(_displays, _selected);
        ShowSelected();
    }

    private void Select(DisplayId id)
    {
        _selected = id;
        ShowSelected();
    }

    private void ShowSelected() =>
        view.ShowSelectedDisplay(SelectedDisplayModel.From(Selected, _displays.Count(d => d.IsActive)));

    private void WithSelected(Action<DisplayInfo> action)
    {
        if (Selected is { } display)
            action(display);
    }

    /// <summary>Applies a change, refreshes the view and, if asked, offers to revert like Windows does.</summary>
    private void Change(string doneMessage, bool confirm, Action change)
    {
        if (_busy)
            return;
        _busy = true;
        view.SetBusy(true);
        try
        {
            var snapshot = confirm ? snapshots.Capture() : null;
            change();
            Reload();
            view.ShowStatus(doneMessage);

            if (snapshot is not null)
            {
                view.SetBusy(false);
                if (!confirmation.ConfirmKeepChanges())
                {
                    snapshots.Restore(snapshot);
                    Reload();
                    view.ShowStatus("Reverted to the previous display settings.");
                }
            }
        }
        catch (DisplayException ex)
        {
            Reload();
            view.ShowStatus(ex.Message, isError: true);
            view.ShowError(ex.Message);
        }
        finally
        {
            view.SetBusy(false);
            _busy = false;
        }
    }
}
