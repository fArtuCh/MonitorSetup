using System.Drawing;

namespace MonitorSetup.Core.Services;

/// <summary>Places a dragged display next to the others, like the arrangement area in Windows Settings.</summary>
public static class ArrangementSnapper
{
    /// <summary>
    /// Moves <paramref name="moved"/> to the closest spot where it shares an edge with one of
    /// <paramref name="others"/> without overlapping any of them.
    /// </summary>
    public static Rectangle Snap(Rectangle moved, IReadOnlyList<Rectangle> others)
    {
        if (others.Count == 0)
            return moved;

        int alignThreshold = Math.Max(moved.Width, moved.Height) / 12;
        var candidates = new List<Rectangle>();
        foreach (var o in others)
        {
            int y = AlignAndClamp(moved.Y, moved.Height, o.Top, o.Bottom, alignThreshold);
            int x = AlignAndClamp(moved.X, moved.Width, o.Left, o.Right, alignThreshold);
            candidates.Add(moved with { X = o.Left - moved.Width, Y = y }); // left of o
            candidates.Add(moved with { X = o.Right, Y = y });               // right of o
            candidates.Add(moved with { X = x, Y = o.Top - moved.Height });  // above o
            candidates.Add(moved with { X = x, Y = o.Bottom });              // below o
        }

        double Distance(Rectangle c) => Math.Pow(c.X - moved.X, 2) + Math.Pow(c.Y - moved.Y, 2);
        var free = candidates.Where(c => !others.Any(o => o.IntersectsWith(c))).ToList();
        return (free.Count > 0 ? free : candidates).MinBy(Distance);
    }

    /// <summary>Keeps a span overlapping [lo, hi] by at least one pixel, snapping to matching edges when close.</summary>
    private static int AlignAndClamp(int start, int length, int lo, int hi, int threshold)
    {
        int[] alignments = [lo, hi - length, lo + (hi - lo - length) / 2];
        foreach (var a in alignments)
            if (Math.Abs(start - a) <= threshold)
                return a;
        return Math.Clamp(start, lo - length + 1, hi - 1);
    }
}
