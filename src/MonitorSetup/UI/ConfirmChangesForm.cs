namespace MonitorSetup.UI;

/// <summary>"Keep these display settings?" prompt that reverts automatically, like Windows does.</summary>
internal sealed class ConfirmChangesForm : Form
{
    private readonly Label _countdown = new() { AutoSize = true, Margin = new Padding(0, 4, 0, 12) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private int _secondsLeft;

    public ConfirmChangesForm(int seconds = 15)
    {
        _secondsLeft = seconds;
        Text = "Monitor Setup";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var title = new Label
        {
            Text = "Keep these display settings?",
            AutoSize = true,
            Font = new Font(Font.FontFamily, Font.Size * 1.3f, FontStyle.Bold),
        };
        var keep = new Button { Text = "Keep changes", DialogResult = DialogResult.OK, AutoSize = true };
        var revert = new Button { Text = "Revert", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.AddRange([revert, keep]);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        layout.Controls.Add(title);
        layout.Controls.Add(_countdown);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        AcceptButton = keep;
        CancelButton = revert;

        UpdateText();
        _timer.Tick += (_, _) =>
        {
            _secondsLeft--;
            UpdateText();
            if (_secondsLeft <= 0)
            {
                _timer.Stop();
                DialogResult = DialogResult.Cancel;
            }
        };
        Shown += (_, _) => _timer.Start();
    }

    private void UpdateText() => _countdown.Text = $"Reverting to previous display settings in {_secondsLeft} seconds.";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }
}
