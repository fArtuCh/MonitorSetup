namespace MonitorSetup.App.UI;

/// <summary>Big number shown in the corner of a display, like "Identify" in Windows Settings.</summary>
internal sealed class IdentifyForm : Form
{
    public IdentifyForm(int number, Rectangle displayBounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(32, 32, 32);
        Opacity = 0.9;

        int size = Math.Max(120, displayBounds.Height / 6);
        Bounds = new Rectangle(displayBounds.X + 40, displayBounds.Bottom - size - 80, (int)(size * 0.9), size);

        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = number.ToString(),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", size * 0.7f, FontStyle.Regular, GraphicsUnit.Pixel),
        });

        var timer = new System.Windows.Forms.Timer { Interval = 2500 };
        timer.Tick += (_, _) => { timer.Dispose(); Close(); };
        Shown += (_, _) => timer.Start();
    }

    protected override bool ShowWithoutActivation => true;
}
