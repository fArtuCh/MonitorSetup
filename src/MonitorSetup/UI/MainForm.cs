using Microsoft.Win32;
using MonitorSetup.Display;

namespace MonitorSetup.UI;

internal sealed class MainForm : Form
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
    private readonly System.Windows.Forms.Timer _reloadDebounce = new() { Interval = 600 };

    private IReadOnlyList<DisplayInfo> _displays = Array.Empty<DisplayInfo>();
    private string? _selectedKey;
    private bool _loading;
    private bool _busy;

    public MainForm()
    {
        Text = "Monitor Setup";
        MinimumSize = new Size(640, 640);
        Size = new Size(760, 780);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        BuildLayout();
        WireEvents();
        ReloadDisplays();
    }

    private static ComboBox DropDown() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };

    private void BuildLayout()
    {
        // Arrangement area
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

        // Selected display settings
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

        // Multiple displays
        _projection.Items.AddRange(["Extend these displays", "Duplicate these displays", "Show only on PC screen", "Show only on second screen"]);
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

    private static Control Spacer() => new Panel { Dock = DockStyle.Top, Height = 10 };

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 16, 3) }, 0, row);
        control.Margin = new Padding(3, 4, 3, 4);
        grid.Controls.Add(control, 1, row);
    }

    private void WireEvents()
    {
        _arrangement.DisplayClicked += (_, d) => Select(d.Key);
        _arrangement.LayoutEdited += (_, _) => _applyLayout.Enabled = _resetLayout.Enabled = true;
        _identify.Click += (_, _) => Identify();
        _refresh.Click += (_, _) => ReloadDisplays();
        _resetLayout.Click += (_, _) => ReloadDisplays();
        _applyLayout.Click += (_, _) => RunChange("Arrangement applied.", confirm: true,
            () => DisplayManager.SetPositions(_displays, _arrangement.Positions));

        _displayPicker.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading && _displayPicker.SelectedItem is DisplayInfo d)
                Select(d.Key);
        };

        _toggleEnabled.Click += (_, _) =>
        {
            if (Selected is not { } d) return;
            bool enable = !d.IsActive;
            RunChange(enable ? $"{d.DisplayName} enabled." : $"{d.DisplayName} disabled.", confirm: true,
                () => DisplayManager.SetEnabled(d, enable));
        };

        _primary.CheckedChanged += (_, _) =>
        {
            if (_loading || Selected is not { IsActive: true } d) return;
            if (!_primary.Checked)
            {
                // Windows has no "un-main"; pick another display instead.
                SetLoading(() => _primary.Checked = true);
                return;
            }
            RunChange($"{d.DisplayName} is now the main display.", confirm: true, () => DisplayManager.SetPrimary(d, _displays));
        };

        _resolution.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || Selected is not { } d || _resolution.SelectedItem is not DisplayModeOption mode) return;
            int rate = mode.RefreshRates.Contains(d.RefreshRate) ? d.RefreshRate : mode.RefreshRates[0];
            RunChange($"Resolution set to {mode}.", confirm: true, () => DisplayManager.SetMode(d, mode.Width, mode.Height, rate));
        };

        _refreshRate.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || Selected is not { } d || _refreshRate.SelectedItem is not RefreshItem rate) return;
            RunChange($"Refresh rate set to {rate}.", confirm: true,
                () => DisplayManager.SetMode(d, d.Bounds.Width, d.Bounds.Height, rate.Hz));
        };

        _orientation.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || Selected is not { } d || _orientation.SelectedItem is not OrientationItem o) return;
            RunChange($"Orientation set to {o}.", confirm: true, () => DisplayManager.SetOrientation(d, o.Value));
        };

        _scale.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || Selected is not { } d || _scale.SelectedItem is not ScaleItem s) return;
            RunChange($"Scale set to {s.Percent}%. Some apps update after you sign out.", confirm: false,
                () => DisplayManager.SetScale(d, s.Percent));
        };

        _hdr.CheckedChanged += (_, _) =>
        {
            if (_loading || Selected is not { } d) return;
            bool on = _hdr.Checked;
            RunChange(on ? "HDR turned on." : "HDR turned off.", confirm: false, () => DisplayManager.SetHdr(d, on));
        };

        _applyProjection.Click += (_, _) =>
        {
            var mode = _projection.SelectedIndex switch
            {
                1 => ProjectionMode.Duplicate,
                2 => ProjectionMode.PcScreenOnly,
                3 => ProjectionMode.SecondScreenOnly,
                _ => ProjectionMode.Extend,
            };
            RunChange($"{_projection.SelectedItem} applied.", confirm: true, () => DisplayManager.SetProjection(mode));
        };

        // Keep the window in sync with changes made elsewhere (Windows Settings, Win+P, plugging a cable).
        _reloadDebounce.Tick += (_, _) =>
        {
            _reloadDebounce.Stop();
            if (!_busy && !_arrangement.IsDirty)
                ReloadDisplays();
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated)
            BeginInvoke(() => { _reloadDebounce.Stop(); _reloadDebounce.Start(); });
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _reloadDebounce.Dispose();
        base.OnFormClosed(e);
    }

    private DisplayInfo? Selected => _displays.FirstOrDefault(d => d.Key == _selectedKey);

    private void ReloadDisplays()
    {
        try
        {
            _displays = DisplayManager.GetDisplays();
        }
        catch (DisplayException ex)
        {
            _displays = Array.Empty<DisplayInfo>();
            SetStatus(ex.Message, error: true);
        }

        if (Selected is null)
            _selectedKey = (_displays.FirstOrDefault(d => d.IsPrimary) ?? _displays.FirstOrDefault())?.Key;

        _arrangement.SetDisplays(_displays, _selectedKey);
        _applyLayout.Enabled = _resetLayout.Enabled = false;

        SetLoading(() =>
        {
            _displayPicker.Items.Clear();
            foreach (var d in _displays)
                _displayPicker.Items.Add(d);
            _displayPicker.SelectedItem = Selected;
        });
        ShowSelected();
    }

    private void Select(string key)
    {
        _selectedKey = key;
        _arrangement.SelectedKey = key;
        SetLoading(() => _displayPicker.SelectedItem = Selected);
        ShowSelected();
    }

    private void ShowSelected()
    {
        SetLoading(() =>
        {
            var d = Selected;
            bool active = d?.IsActive == true;
            int activeCount = _displays.Count(x => x.IsActive);

            _state.Text = d is null ? "No display" : active ? "Enabled" : "Disabled";
            _toggleEnabled.Text = active ? "Disable this display" : "Enable this display";
            _toggleEnabled.Enabled = d is not null && (!active || activeCount > 1);

            _primary.Checked = d?.IsPrimary == true;
            _primary.Enabled = active && activeCount > 1 && d?.IsPrimary != true;

            _resolution.Items.Clear();
            _refreshRate.Items.Clear();
            _orientation.Items.Clear();
            _scale.Items.Clear();

            if (d is { IsActive: true })
            {
                foreach (var mode in d.Modes)
                    _resolution.Items.Add(mode);
                var current = d.Modes.FirstOrDefault(m => m.Width == d.Bounds.Width && m.Height == d.Bounds.Height);
                if (current is not null)
                {
                    _resolution.SelectedItem = current;
                    foreach (var hz in current.RefreshRates)
                        _refreshRate.Items.Add(new RefreshItem(hz));
                    _refreshRate.SelectedItem = _refreshRate.Items.Cast<RefreshItem>().FirstOrDefault(r => r.Hz == d.RefreshRate);
                }

                foreach (var o in Enum.GetValues<DisplayOrientation>())
                    _orientation.Items.Add(new OrientationItem(o));
                _orientation.SelectedIndex = (int)d.Orientation;

                foreach (var s in d.ScaleOptions)
                    _scale.Items.Add(new ScaleItem(s, s == d.RecommendedScale));
                _scale.SelectedItem = _scale.Items.Cast<ScaleItem>().FirstOrDefault(s => s.Percent == d.CurrentScale);
            }

            _resolution.Enabled = _refreshRate.Enabled = _orientation.Enabled = active;
            _scale.Enabled = active && _scale.Items.Count > 0;

            _hdr.Visible = d?.HdrSupported == true;
            _hdr.Checked = d?.HdrEnabled == true;

            _details.Text = d switch
            {
                null => "",
                { IsActive: true } => $"{d.Connection} · {d.GdiName} · position ({d.Bounds.X}, {d.Bounds.Y}) · {d.BitsPerPixel}-bit",
                _ => $"{d.Connection} · connected but not part of the desktop",
            };
        });
    }

    private void SetLoading(Action action)
    {
        bool was = _loading;
        _loading = true;
        try { action(); }
        finally { _loading = was; }
    }

    /// <summary>Applies a change, refreshes the view and, if asked, offers to revert like Windows does.</summary>
    private void RunChange(string doneMessage, bool confirm, Action change)
    {
        if (_busy) return;
        _busy = true;
        UseWaitCursor = true;
        try
        {
            var snapshot = confirm ? DisplayManager.TakeSnapshot() : null;
            change();
            ReloadDisplays();
            SetStatus(doneMessage);

            if (snapshot is not null)
            {
                using var dialog = new ConfirmChangesForm();
                UseWaitCursor = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    DisplayManager.Restore(snapshot);
                    ReloadDisplays();
                    SetStatus("Reverted to the previous display settings.");
                }
            }
        }
        catch (DisplayException ex)
        {
            ReloadDisplays();
            SetStatus(ex.Message, error: true);
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
            _busy = false;
        }
    }

    private void Identify()
    {
        foreach (var d in _displays.Where(d => d.IsActive))
            new IdentifyForm(d.Number, d.Bounds).Show();
    }

    private void SetStatus(string text, bool error = false)
    {
        _status.Text = text;
        _status.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
    }

    private sealed record RefreshItem(int Hz)
    {
        public override string ToString() => $"{Hz} Hz";
    }

    private sealed record ScaleItem(int Percent, bool Recommended)
    {
        public override string ToString() => Recommended ? $"{Percent}% (Recommended)" : $"{Percent}%";
    }

    private sealed record OrientationItem(DisplayOrientation Value)
    {
        public override string ToString() => Value switch
        {
            DisplayOrientation.Landscape => "Landscape",
            DisplayOrientation.Portrait => "Portrait",
            DisplayOrientation.LandscapeFlipped => "Landscape (flipped)",
            DisplayOrientation.PortraitFlipped => "Portrait (flipped)",
            _ => Value.ToString(),
        };
    }
}
