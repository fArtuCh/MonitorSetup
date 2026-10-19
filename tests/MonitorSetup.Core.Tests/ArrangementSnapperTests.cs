using System.Drawing;
using MonitorSetup.Core.Services;

namespace MonitorSetup.Core.Tests;

public class ArrangementSnapperTests
{
    private static readonly Rectangle Main = new(0, 0, 3840, 2160);

    [Fact]
    public void Leaves_a_lone_display_where_it_was_dropped()
    {
        var r = new Rectangle(123, 456, 1920, 1080);
        Assert.Equal(r, ArrangementSnapper.Snap(r, []));
    }

    [Fact]
    public void Snaps_to_the_right_edge_and_aligns_the_top()
    {
        var dropped = new Rectangle(3900, 40, 1920, 1080);
        Assert.Equal(new Rectangle(3840, 0, 1920, 1080), ArrangementSnapper.Snap(dropped, [Main]));
    }

    [Fact]
    public void Snaps_to_the_left_edge_when_dropped_on_the_left()
    {
        var dropped = new Rectangle(-1800, 900, 1920, 1080);
        var snapped = ArrangementSnapper.Snap(dropped, [Main]);
        Assert.Equal(-1920, snapped.X);
        Assert.Equal(900, snapped.Y);
    }

    [Fact]
    public void Snaps_below_when_dropped_under_the_display()
    {
        var dropped = new Rectangle(900, 2300, 1920, 1080);
        var snapped = ArrangementSnapper.Snap(dropped, [Main]);
        Assert.Equal(2160, snapped.Y);
    }

    [Fact]
    public void Pulls_a_far_away_display_back_so_edges_touch()
    {
        var dropped = new Rectangle(9000, 5000, 1920, 1080);
        var snapped = ArrangementSnapper.Snap(dropped, [Main]);
        Assert.True(snapped.Left == Main.Right || snapped.Top == Main.Bottom);
        Assert.False(snapped.IntersectsWith(Main));
    }

    [Fact]
    public void Never_overlaps_other_displays_when_a_free_spot_exists()
    {
        var right = new Rectangle(3840, 0, 1920, 1080);
        var dropped = new Rectangle(3850, 100, 1920, 1080); // dropped on top of `right`
        var snapped = ArrangementSnapper.Snap(dropped, [Main, right]);
        Assert.False(snapped.IntersectsWith(Main));
        Assert.False(snapped.IntersectsWith(right));
    }
}
