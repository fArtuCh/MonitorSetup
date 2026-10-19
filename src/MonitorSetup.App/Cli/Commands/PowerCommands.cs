using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using MonitorSetup.Core.Services;

namespace MonitorSetup.App.Cli.Commands;

internal sealed class EnableCommand(IDisplayReader reader, IDisplayPowerService power) : DisplayCommandBase(reader)
{
    public override string Name => "enable";
    public override string Usage => "<n>";
    public override string Description => "Turn display n on";
    protected override int ExtraArgumentCount => 0;

    protected override string Execute(DisplayInfo display, IReadOnlyList<string> args)
    {
        power.Enable(display.Id);
        return $"Display {display.Number} enabled.";
    }
}

internal sealed class DisableCommand(IDisplayReader reader, IDisplayPowerService power) : DisplayCommandBase(reader)
{
    public override string Name => "disable";
    public override string Usage => "<n>";
    public override string Description => "Turn display n off";
    protected override int ExtraArgumentCount => 0;

    protected override string Execute(DisplayInfo display, IReadOnlyList<string> args)
    {
        power.Disable(display.Id);
        return $"Display {display.Number} disabled.";
    }
}

internal sealed class ToggleCommand(IDisplayReader reader, IDisplayPowerService power) : DisplayCommandBase(reader)
{
    public override string Name => "toggle";
    public override string Usage => "<n>";
    public override string Description => "Turn display n on if it is off, and off if it is on";
    protected override int ExtraArgumentCount => 0;

    protected override string Execute(DisplayInfo display, IReadOnlyList<string> args)
    {
        if (display.IsActive)
        {
            power.Disable(display.Id);
            return $"Display {display.Number} disabled.";
        }
        power.Enable(display.Id);
        return $"Display {display.Number} enabled.";
    }
}
