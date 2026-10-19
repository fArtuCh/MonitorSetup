using System.Drawing.Drawing2D;
using MonitorSetup.Display;

namespace MonitorSetup.UI;

/// <summary>
/// Shows the active displays as rectangles, like the arrangement area in Windows Settings,
/// and lets the user drag them around. Dropped displays snap to the edge of the nearest neighbour.
/// </summary>
internal sealed class ArrangementView : Control
{
    private readonly Dictionary<string, Rectangle> _layout = new();
    private IReadOnlyList<DisplayInfo> _displays = Array.Empty<DisplayInfo>();
    private string? _selectedKey;

    private float _scale = 1;
    private PointF _origin;

    private string? _dragKey;
    private Point _dragStartMouse;
    private Rectangle _dragStartRect;

    public event EventHandler<DisplayInfo>? DisplayClicked;
    public event EventHandler? LayoutEdited;

    public ArrangementView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
    }

    /// <summary>True when the user moved displays and has not applied or reset yet.</summary>
    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<string, Point> Positions => _layout.ToDictionary(kv => kv.Key, kv => kv.Value.Location);

    public void SetDisplays(IReadOnlyList<DisplayInfo> displays, string? selectedKey)
    {
        _displays = displays.Where(d => d.IsActive).ToList();
        _layout.Clear();
        foreach (var d in _displays)
            _layout[d.Key] = d.Bounds;
        _selectedKey = selectedKey;
        IsDirty = false;
        ComputeTransform();
        Invalidate();
    }

    public string? SelectedKey
    {
        get => _selectedKey;
        set { _selectedKey = value; Invalidate(); }
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
        int margin = 30;
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
        foreach (var d in _displays.OrderBy(d => d.Key == _dragKey))
        {
            var r = ToScreen(_layout[d.Key]);
            r.Inflate(-2, -2);
            bool selected = d.Key == _selectedKey;

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
        _displays.LastOrDefault(d => ToScreen(_layout[d.Key]).Contains(p));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var hit = HitTest(e.Location);
        if (hit is null || e.Button != MouseButtons.Left)
            return;

        _selectedKey = hit.Key;
        DisplayClicked?.Invoke(this, hit);
        if (_displays.Count > 1)
        {
            _dragKey = hit.Key;
            _dragStartMouse = e.Location;
            _dragStartRect = _layout[hit.Key];
            Capture = true;
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragKey is null)
        {
            Cursor = HitTest(e.Location) is null || _displays.Count < 2 ? Cursors.Default : Cursors.SizeAll;
            return;
        }

        int dx = (int)Math.Round((e.X - _dragStartMouse.X) / _scale);
        int dy = (int)Math.Round((e.Y - _dragStartMouse.Y) / _scale);
        var r = _dragStartRect;
        r.Offset(dx, dy);
        _layout[_dragKey] = r;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragKey is null)
            return;

        var moved = _layout[_dragKey];
        var snapped = Snap(moved, _layout.Where(kv => kv.Key != _dragKey).Select(kv => kv.Value).ToList());
        _layout[_dragKey] = snapped;
        if (snapped.Location != _dragStartRect.Location)
        {
            IsDirty = true;
            LayoutEdited?.Invoke(this, EventArgs.Empty);
        }
        _dragKey = null;
        Capture = false;
        Invalidate();
    }

    /// <summary>
    /// Moves <paramref name="r"/> to the closest spot where it touches one of the other displays
    /// along a full edge without overlapping any of them.
    /// </summary>
    internal static Rectangle Snap(Rectangle r, IReadOnlyList<Rectangle> others)
    {
        if (others.Count == 0)
            return r;

        int alignThreshold = Math.Max(r.Width, r.Height) / 12;
        var candidates = new List<Rectangle>();
        foreach (var o in others)
        {
            int y = AlignAndClamp(r.Y, r.Height, o.Top, o.Bottom, alignThreshold);
            int x = AlignAndClamp(r.X, r.Width, o.Left, o.Right, alignThreshold);
            candidates.Add(new Rectangle(o.Left - r.Width, y, r.Width, r.Height)); // left of o
            candidates.Add(new Rectangle(o.Right, y, r.Width, r.Height));          // right of o
            candidates.Add(new Rectangle(x, o.Top - r.Height, r.Width, r.Height)); // above o
            candidates.Add(new Rectangle(x, o.Bottom, r.Width, r.Height));         // below o
        }

        double Distance(Rectangle c) => Math.Pow(c.X - r.X, 2) + Math.Pow(c.Y - r.Y, 2);
        var free = candidates.Where(c => !others.Any(o => o.IntersectsWith(c))).ToList();
        return (free.Count > 0 ? free : candidates).MinBy(Distance);
    }

    /// <summary>Keeps a span overlapping [lo, hi] by at least one pixel and snaps it to matching edges when close.</summary>
    private static int AlignAndClamp(int start, int length, int lo, int hi, int threshold)
    {
        int[] alignments = [lo, hi - length, lo + (hi - lo - length) / 2];
        foreach (var a in alignments)
            if (Math.Abs(start - a) <= threshold)
                return a;
        return Math.Clamp(start, lo - length + 1, hi - 1);
    }
}
