using System.Drawing;
using MonitorSetup.Core.Models;
using MonitorSetup.Core.Services;

namespace MonitorSetup.Core.Tests;

public class DisplayPowerServiceTests
{
    private static readonly DisplayId Main = new(1, 1);
    private static readonly DisplayId Side = new(1, 2);

    private readonly FakeDisplays _fake = new();
    private readonly InMemoryStateStore _store = new();
    private readonly DisplayPowerService _power;

    public DisplayPowerServiceTests()
    {
        _fake.Displays.Add(FakeDisplays.Display(1, new Rectangle(0, 0, 3840, 2160)));
        _fake.Displays.Add(FakeDisplays.Display(2, new Rectangle(3840, 244, 1920, 1080), hz: 60));
        _power = new DisplayPowerService(_fake, _fake, _fake, new DisplayLayoutService(_fake, _fake), _store);
    }

    [Fact]
    public void Disable_remembers_the_display_and_turns_it_off()
    {
        _power.Disable(Side);

        Assert.Contains("deactivate 2", _fake.Calls);
        var saved = Assert.Single(_store.States).Value;
        Assert.Equal((3840, 244, 60), (saved.X, saved.Y, saved.RefreshRate));
    }

    [Fact]
    public void Disable_refuses_to_turn_off_the_last_display()
    {
        _power.Disable(Side);
        Assert.Throws<DisplayException>(() => _power.Disable(Main));
    }

    [Fact]
    public void Enable_puts_the_display_back_where_it_was_with_the_same_refresh_rate()
    {
        _power.Disable(Side);
        _power.Enable(Side);

        var side = _fake.Displays.Single(d => d.Id == Side);
        Assert.True(side.IsActive);
        Assert.Equal(60, side.RefreshRate);
        Assert.Equal(new Point(3840, 244), side.Bounds.Location);
    }

    [Fact]
    public void Enable_does_not_move_the_display_onto_another_one()
    {
        _power.Disable(Side);
        // Main display grew into the remembered spot while the side display was off.
        _fake.SetMode(Main, 5000, 2160, 60);
        _fake.Calls.Clear();

        _power.Enable(Side);

        Assert.DoesNotContain("positions", _fake.Calls);
    }

    [Fact]
    public void Enable_does_nothing_when_the_display_is_already_on()
    {
        _power.Enable(Main);
        Assert.Empty(_fake.Calls);
    }
}
