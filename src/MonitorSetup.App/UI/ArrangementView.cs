using System.ComponentModel;
using System.Drawing.Drawing2D;
using MonitorSetup.Core.Models;
using MonitorSetup.Core.Services;

namespace MonitorSetup.App.UI;

/// <summary>
/// Shows the active displays as rectangles, like the arrangement area in Windows Settings,
/// and lets the user drag them around. Dropped displays snap to the edge of the nearest neighbour.
/// </summary>
internal sealed class ArrangementView : Control
{
    private readonly Dictionary<DisplayId, Rectangle> _layout = [];
    private IReadOnlyList<DisplayInfo> _displays = [];
    private DisplayId? _selected;

    private float _scale = 1;
    private PointF _origin;

    private DisplayId? _dragging;
    private Point _dragStartMouse;
    private Rectangle _dragStartRect;

    public event EventHandler<DisplayId>? DisplayClicked;
    public event EventHandler? LayoutEdited;

    public ArrangementView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
    }

    /// <summary>True when the user moved displays and has not applied or reset yet.</summary>
    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<DisplayId, Point> Positions => _layout.ToDictionary(kv => kv.Key, kv => kv.Value.Location);

    public void SetDisplays(IReadOnlyList<DisplayInfo> displays, DisplayId? selected)
    {
        _displays = displays.Where(d => d.IsActive).ToList();
        _layout.Clear();
        foreach (var d in _displays)
            _layout[d.Id] = d.Bounds;
        _selected = selected;
        IsDirty = false;
        ComputeTransform();
        Invalidate();
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DisplayId? Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ComputeTransform();
    }

    private void ComputeTransform()
    {
        if (_layout.Count == 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        var all = _layout.Values.Aggregate(Rectangle.Union);
        // Leave room around the displays so there is space to drag them.
        const int margin = 30;
        float sx = (ClientSize.Width - 2 * margin) / (float)(all.Width * 1.6);
        float sy = (ClientSize.Height - 2 * margin) / (float)(all.Height * 1.6);
        _scale = Math.Max(0.01f, Math.Min(sx, sy));
        _origin = new PointF(
            ClientSize.Width / 2f - (all.X + all.Width / 2f) * _scale,
            ClientSize.Height / 2f - (all.Y + all.Height / 2f) * _scale);
    }

    private RectangleF ToScreen(Rectangle r) =>
        new(_origin.X + r.X * _scale, _origin.Y + r.Y * _scale, r.Width * _scale, r.Height * _scale);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(SystemColors.ControlLight);

        if (_displays.Count == 0)
        {
            TextRenderer.DrawText(g, "No active displays", Font, ClientRectangle, SystemColors.GrayText);
            return;
        }

        using var numberFont = new Font(Font.FontFamily, Math.Max(10, Font.Size * 2.2f), FontStyle.Bold);
        foreach (var d in _displays.OrderBy(d => d.Id == _dragging))
        {
            var r = ToScreen(_layout[d.Id]);
            r.Inflate(-2, -2);
            bool selected = d.Id == _selected;

            using var fill = new SolidBrush(selected ? Color.FromArgb(0, 103, 192) : Color.FromArgb(96, 110, 128));
            using var path = RoundedRect(r, 6);
            g.FillPath(fill, path);
            if (selected)
            {
                using var pen = new Pen(Color.FromArgb(153, 204, 255), 3);
                g.DrawPath(pen, path);
            }

            var rect = Rectangle.Round(r);
            TextRenderer.DrawText(g, d.Number.ToString(), numberFont, rect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (d.IsPrimary)
                TextRenderer.DrawText(g, "Main", Font, new Rectangle(rect.X + 6, rect.Y + 4, rect.Width - 12, Font.Height), Color.White,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0)
        {
            p.AddRectangle(r);
            return p;
        }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private DisplayInfo? HitTest(Point p) =>
        _displays.LastOrDefault(d => ToScreen(_layout[d.Id]).Contains(p));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var hit = HitTest(e.Location);
        if (hit is null || e.Button != MouseButtons.Left)
            return;

        _selected = hit.Id;
        DisplayClicked?.Invoke(this, hit.Id);
        if (_displays.Count > 1)
        {
            _dragging = hit.Id;
            _dragStartMouse = e.Location;
            _dragStartRect = _layout[hit.Id];
            Capture = true;
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging is not { } id)
        {
            Cursor = HitTest(e.Location) is null || _displays.Count < 2 ? Cursors.Default : Cursors.SizeAll;
            return;
        }

        var r = _dragStartRect;
        r.Offset((int)Math.Round((e.X - _dragStartMouse.X) / _scale), (int)Math.Round((e.Y - _dragStartMouse.Y) / _scale));
        _layout[id] = r;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging is not { } id)
            return;

        var others = _layout.Where(kv => kv.Key != id).Select(kv => kv.Value).ToList();
        var snapped = ArrangementSnapper.Snap(_layout[id], others);
        _layout[id] = snapped;
        if (snapped.Location != _dragStartRect.Location)
        {
            IsDirty = true;
            LayoutEdited?.Invoke(this, EventArgs.Empty);
        }
        _dragging = null;
        Capture = false;
        Invalidate();
    }
}
