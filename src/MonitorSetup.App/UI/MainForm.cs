using Microsoft.Win32;
using MonitorSetup.Core.Models;

namespace MonitorSetup.App.UI;

/// <summary>Passive view: lays out controls and raises <see cref="IMainView"/> events. All logic is in <see cref="MainPresenter"/>.</summary>
internal sealed class MainForm : Form, IMainView
{
    private readonly ArrangementView _arrangement = new() { Dock = DockStyle.Fill };
    private readonly Button _identify = new() { Text = "Identify", AutoSize = true };
    private readonly Button _refresh = new() { Text = "Refresh", AutoSize = true };
    private readonly Button _applyLayout = new() { Text = "Apply arrangement", AutoSize = true, Enabled = false };
    private readonly Button _resetLayout = new() { Text = "Reset", AutoSize = true, Enabled = false };

    private readonly ComboBox _displayPicker = DropDown();
    private readonly Label _state = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button _toggleEnabled = new() { AutoSize = true };
    private readonly CheckBox _primary = new() { Text = "Make this my main display", AutoSize = true };
    private readonly ComboBox _resolution = DropDown();
    private readonly ComboBox _refreshRate = DropDown();
    private readonly ComboBox _orientation = DropDown();
    private readonly ComboBox _scale = DropDown();
    private readonly CheckBox _hdr = new() { Text = "Use HDR", AutoSize = true };
    private readonly Label _details = new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(520, 0) };

    private readonly ComboBox _projection = DropDown();
    private readonly Button _applyProjection = new() { Text = "Apply", AutoSize = true };

    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly System.Windows.Forms.Timer _externalChangeDebounce = new() { Interval = 600 };

    /// <summary>Set while the form fills controls, so programmatic changes are not reported as user actions.</summary>
    private bool _updating;

    public event EventHandler? Loaded;
    public event EventHandler? RefreshRequested;
    public event EventHandler? IdentifyRequested;
    public event EventHandler? DisplaysChangedExternally;
    public event EventHandler<DisplayId>? DisplaySelected;
    public event EventHandler? ToggleEnabledRequested;
    public event EventHandler? MakePrimaryRequested;
    public event EventHandler<IReadOnlyDictionary<DisplayId, Point>>? ApplyArrangementRequested;
    public event EventHandler? ResetArrangementRequested;
    public event EventHandler<DisplayMode>? ResolutionSelected;
    public event EventHandler<int>? RefreshRateSelected;
    public event EventHandler<DisplayOrientation>? OrientationSelected;
    public event EventHandler<int>? ScaleSelected;
    public event EventHandler<bool>? HdrToggled;
    public event EventHandler<ProjectionMode>? ProjectionRequested;

    public MainForm()
    {
        Text = "Monitor Setup";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        MinimumSize = new Size(640, 640);
        Size = new Size(760, 840);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        BuildLayout();
        WireEvents();
    }

    public bool HasUnappliedArrangement => _arrangement.IsDirty;

    private static ComboBox DropDown() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };

    #region Layout

    private void BuildLayout()
    {
        var arrangeButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        arrangeButtons.Controls.AddRange([_identify, _refresh, _applyLayout, _resetLayout]);
        var arrangeHint = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "Select a display to change its settings. Drag displays to rearrange them, then click Apply arrangement.",
            Padding = new Padding(0, 0, 0, 6),
        };
        var arrangeGroup = new GroupBox { Text = "Rearrange your displays", Dock = DockStyle.Top, Height = 300, Padding = new Padding(10) };
        arrangeGroup.Controls.Add(_arrangement);
        arrangeGroup.Controls.Add(arrangeButtons);
        arrangeGroup.Controls.Add(arrangeHint);

        var stateRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        stateRow.Controls.AddRange([_state, _toggleEnabled]);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(grid, "Display", _displayPicker);
        AddRow(grid, "Status", stateRow);
        AddRow(grid, "", _primary);
        AddRow(grid, "Display resolution", _resolution);
        AddRow(grid, "Refresh rate", _refreshRate);
        AddRow(grid, "Display orientation", _orientation);
        AddRow(grid, "Scale", _scale);
        AddRow(grid, "", _hdr);
        AddRow(grid, "", _details);

        var settingsGroup = new GroupBox { Text = "Display settings", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        settingsGroup.Controls.Add(grid);

        _projection.Items.AddRange([
            new Choice<ProjectionMode>(ProjectionMode.Extend, "Extend these displays"),
            new Choice<ProjectionMode>(ProjectionMode.Duplicate, "Duplicate these displays"),
            new Choice<ProjectionMode>(ProjectionMode.PcScreenOnly, "Show only on PC screen"),
            new Choice<ProjectionMode>(ProjectionMode.SecondScreenOnly, "Show only on second screen"),
        ]);
        _projection.SelectedIndex = 0;
        var projectionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        projectionRow.Controls.AddRange([_projection, _applyProjection]);
        var multiGroup = new GroupBox { Text = "Multiple displays", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        multiGroup.Controls.Add(projectionRow);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
        // Docked controls are laid out in reverse order of addition.
        content.Controls.Add(multiGroup);
        content.Controls.Add(Spacer());
        content.Controls.Add(settingsGroup);
        content.Controls.Add(Spacer());
        content.Controls.Add(arrangeGroup);

        Controls.Add(content);
        Controls.Add(statusStrip);
    }

    private static Panel Spacer() => new() { Dock = DockStyle.Top, Height = 10 };

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 16, 3) }, 0, row);
        control.Margin = new Padding(3, 4, 3, 4);
        grid.Controls.Add(control, 1, row);
    }

    #endregion

    #region Events

    private void WireEvents()
    {
        Shown += (_, _) => Loaded?.Invoke(this, EventArgs.Empty);

        _arrangement.DisplayClicked += (_, id) => DisplaySelected?.Invoke(this, id);
        _arrangement.LayoutEdited += (_, _) => _applyLayout.Enabled = _resetLayout.Enabled = true;
        _identify.Click += (_, _) => IdentifyRequested?.Invoke(this, EventArgs.Empty);
        _refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _resetLayout.Click += (_, _) => ResetArrangementRequested?.Invoke(this, EventArgs.Empty);
        _applyLayout.Click += (_, _) => ApplyArrangementRequested?.Invoke(this, _arrangement.Positions);
        _toggleEnabled.Click += (_, _) => ToggleEnabledRequested?.Invoke(this, EventArgs.Empty);
        _applyProjection.Click += (_, _) =>
        {
            if (_projection.SelectedItem is Choice<ProjectionMode> choice)
                ProjectionRequested?.Invoke(this, choice.Value);
        };

        OnUserChange(_displayPicker, () => _displayPicker.SelectedItem is DisplayInfo d ? () => DisplaySelected?.Invoke(this, d.Id) : null);
        OnUserChange(_resolution, () => _resolution.SelectedItem is DisplayMode m ? () => ResolutionSelected?.Invoke(this, m) : null);
        OnUserChange(_refreshRate, () => _refreshRate.SelectedItem is Choice<int> r ? () => RefreshRateSelected?.Invoke(this, r.Value) : null);
        OnUserChange(_orientation, () => _orientation.SelectedItem is Choice<DisplayOrientation> o ? () => OrientationSelected?.Invoke(this, o.Value) : null);
        OnUserChange(_scale, () => _scale.SelectedItem is Choice<int> s ? () => ScaleSelected?.Invoke(this, s.Value) : null);

        _primary.CheckedChanged += (_, _) =>
        {
            if (_updating) return;
            if (_primary.Checked)
                MakePrimaryRequested?.Invoke(this, EventArgs.Empty);
            else
                Updating(() => _primary.Checked = true); // there is no "un-main"; pick another display instead
        };
        _hdr.CheckedChanged += (_, _) =>
        {
            if (!_updating)
                HdrToggled?.Invoke(this, _hdr.Checked);
        };

        // Follow changes made elsewhere (Windows Settings, Win+P, plugging in a cable).
        _externalChangeDebounce.Tick += (_, _) =>
        {
            _externalChangeDebounce.Stop();
            DisplaysChangedExternally?.Invoke(this, EventArgs.Empty);
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnUserChange(ComboBox combo, Func<Action?> raise) =>
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (!_updating)
                raise()?.Invoke();
        };

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated)
            BeginInvoke(() => { _externalChangeDebounce.Stop(); _externalChangeDebounce.Start(); });
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _externalChangeDebounce.Dispose();
        base.OnFormClosed(e);
    }

    #endregion

    #region IMainView

    public void ShowDisplays(IReadOnlyList<DisplayInfo> displays, DisplayId? selected)
    {
        _arrangement.SetDisplays(displays, selected);
        _applyLayout.Enabled = _resetLayout.Enabled = false;
        Updating(() =>
        {
            _displayPicker.Items.Clear();
            _displayPicker.Items.AddRange([.. displays]);
            _displayPicker.SelectedItem = displays.FirstOrDefault(d => d.Id == selected);
        });
    }

    public void ShowSelectedDisplay(SelectedDisplayModel model) => Updating(() =>
    {
        _arrangement.Selected = model.Id;
        _displayPicker.SelectedItem = _displayPicker.Items.Cast<DisplayInfo>().FirstOrDefault(d => d.Id == model.Id);

        _state.Text = model.StateText;
        _toggleEnabled.Text = model.ToggleText;
        _toggleEnabled.Enabled = model.CanToggle;
        _primary.Checked = model.IsPrimary;
        _primary.Enabled = model.CanMakePrimary;

        Fill(_resolution, model.Modes.Cast<object>(), model.CurrentMode);
        Fill(_refreshRate,
            (model.CurrentMode?.RefreshRates ?? []).Select(hz => new Choice<int>(hz, $"{hz} Hz")),
            model.CurrentRefreshRate is int rate ? new Choice<int>(rate, $"{rate} Hz") : null);
        Fill(_orientation,
            model.CanChangeMode ? Enum.GetValues<DisplayOrientation>().Select(OrientationChoice) : [],
            model.CanChangeMode ? OrientationChoice(model.Orientation) : null);
        Fill(_scale,
            model.ScaleOptions.Select(p => ScaleChoice(p, model.RecommendedScale)),
            model.CurrentScale is int s ? ScaleChoice(s, model.RecommendedScale) : null);

        _resolution.Enabled = _refreshRate.Enabled = _orientation.Enabled = model.CanChangeMode;
        _scale.Enabled = _scale.Items.Count > 0;
        _hdr.Visible = model.HdrSupported;
        _hdr.Checked = model.HdrEnabled;
        _details.Text = model.Details;
    });

    public void ShowStatus(string message, bool isError = false)
    {
        _status.Text = message;
        _status.ForeColor = isError ? Color.Firebrick : SystemColors.ControlText;
    }

    public void ShowError(string message) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public void SetBusy(bool busy) => UseWaitCursor = busy;

    #endregion

    private void Updating(Action action)
    {
        bool was = _updating;
        _updating = true;
        try { action(); }
        finally { _updating = was; }
    }

    private static void Fill(ComboBox combo, IEnumerable<object> items, object? selected)
    {
        combo.Items.Clear();
        combo.Items.AddRange([.. items]);
        combo.SelectedItem = selected;
    }

    private static Choice<DisplayOrientation> OrientationChoice(DisplayOrientation o) => new(o, o switch
    {
        DisplayOrientation.Landscape => "Landscape",
        DisplayOrientation.Portrait => "Portrait",
        DisplayOrientation.LandscapeFlipped => "Landscape (flipped)",
        DisplayOrientation.PortraitFlipped => "Portrait (flipped)",
        _ => o.ToString(),
    });

    private static Choice<int> ScaleChoice(int percent, int? recommended) =>
        new(percent, percent == recommended ? $"{percent}% (Recommended)" : $"{percent}%");

    /// <summary>Combo box item with a value and its label; equality is by value so it can be re-selected.</summary>
    private sealed record Choice<T>(T Value, string Label)
    {
        public bool Equals(Choice<T>? other) => other is not null && EqualityComparer<T>.Default.Equals(Value, other.Value);
        public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(Value!);
        public override string ToString() => Label;
    }
}
