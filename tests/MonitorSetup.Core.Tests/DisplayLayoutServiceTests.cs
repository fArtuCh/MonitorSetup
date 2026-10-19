using System.Drawing;
using MonitorSetup.Core.Models;
using MonitorSetup.Core.Services;

namespace MonitorSetup.Core.Tests;

public class DisplayLayoutServiceTests
{
    private readonly FakeDisplays _fake = new();
    private readonly DisplayLayoutService _layout;

    public DisplayLayoutServiceTests()
    {
        _fake.Displays.Add(FakeDisplays.Display(1, new Rectangle(0, 0, 3840, 2160)));
        _fake.Displays.Add(FakeDisplays.Display(2, new Rectangle(3840, 0, 1920, 1080)));
        _fake.Displays.Add(FakeDisplays.Display(3, Rectangle.Empty, active: false));
        _layout = new DisplayLayoutService(_fake, _fake);
    }

    [Fact]
    public void MakePrimary_shifts_everything_so_the_new_main_display_is_at_the_origin()
    {
        _layout.MakePrimary(new DisplayId(1, 2));

        Assert.Equal(new DisplayId(1, 2), _fake.LastNewPrimary);
        Assert.Equal(new Point(0, 0), _fake.LastPositions![new DisplayId(1, 2)]);
        Assert.Equal(new Point(-3840, 0), _fake.LastPositions[new DisplayId(1, 1)]);
        Assert.DoesNotContain(new DisplayId(1, 3), _fake.LastPositions.Keys);
    }

    [Fact]
    public void MakePrimary_rejects_a_disabled_display()
    {
        Assert.Throws<DisplayException>(() => _layout.MakePrimary(new DisplayId(1, 3)));
    }

    [Fact]
    public void MoveDisplays_keeps_the_main_display_at_the_origin()
    {
        _layout.MoveDisplays(new Dictionary<DisplayId, Point>
        {
            [new DisplayId(1, 1)] = new(100, 50),
            [new DisplayId(1, 2)] = new(100 - 1920, 50),
        });

        Assert.Equal(new Point(0, 0), _fake.LastPositions![new DisplayId(1, 1)]);
        Assert.Equal(new Point(-1920, 0), _fake.LastPositions[new DisplayId(1, 2)]);
        Assert.Null(_fake.LastNewPrimary);
    }
}
