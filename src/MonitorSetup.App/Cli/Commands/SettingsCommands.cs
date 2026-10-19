using System.Drawing;
using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using MonitorSetup.Core.Services;

namespace MonitorSetup.App.Cli.Commands;

internal sealed class ModeCommand(IDisplayReader reader, IDisplayModeSetter modes) : DisplayCommandBase(reader)
{
    public override string Name => "mode";
    public override string Usage => "<n> <width> <height> <hz>";
    public override string Description => "Set resolution and refresh rate";
    protected override int ExtraArgumentCount => 3;

    protected override string Execute(DisplayInfo display, IReadOnlyList<string> args)
    {
        int width = ParseInt(args[0]), height = ParseInt(args[1]), hz = ParseInt(args[2]);
        modes.SetMode(display.Id, width, height, hz);
        return $"Display {display.Number} set to {width}x{height} @ {hz} Hz.";
    }
}

internal sealed class RotateCommand(IDisplayReader reader, IDisplayModeSetter modes) : DisplayCommandBase(reader)
{
    public override string Name => "rotate";
    public override string Usage => "<n> <landscape|portrait|landscapeflipped|portraitflipped>";
    public override string Description => "Set orientation";
    protected override int ExtraArgumentCount => 1;

    protected override string Execute(DisplayInfo display, IReadOnlyList<string> args)
    {
        if (!Enum.TryParse<DisplayOrientation>(args[0], ignoreCase: true, out var orientation) || !Enum.IsDefined(orientation))
            throw new UsageException();
        modes.SetOrientation(display.Id, orientation);
        return $"Display {display.Number} set to {orientation}.";
    }
}

internal sealed class ScaleCommand(IDisplayReader reader, IDisplayScaleSetter scale) : DisplayCommandBase(reader)
{
    public override string Name => "scale";
    public override string Usage => "<n> <percent>";
    public override string Description => "Set scale, e.g. 125";
    protected override int ExtraArgumentCount => 1;

    protected override string Execute(DisplayInfo display, IReadOnlyList<string> args)
    {
        int percent = ParseInt(args[0].TrimEnd('%'));
        scale.SetScale(display.Id, percent);
        return $"Display {display.Number} scale set to {percent}%.";
    }
}

internal sealed class MoveCommand(IDisplayReader reader, IDisplayLayoutService layout) : DisplayCommandBase(reader)
{
    private readonly IDisplayReader _reader = reader;

    public override string Name => "move";
    public override string Usage => "<n> <x> <y>";
    public override string Description => "Move a display on the desktop";
    protected override int ExtraArgumentCount => 2;

    protected override string Execute(DisplayInfo display, IReadOnlyList<string> args)
    {
        var target = new Point(ParseInt(args[0]), ParseInt(args[1]));
        var positions = _reader.GetDisplays().Where(d => d.IsActive).ToDictionary(d => d.Id, d => d.Bounds.Location);
        positions[display.Id] = target;
        layout.MoveDisplays(positions);
        return $"Display {display.Number} moved to ({target.X},{target.Y}).";
    }
}

internal sealed class PrimaryCommand(IDisplayReader reader, IDisplayLayoutService layout) : DisplayCommandBase(reader)
{
    public override string Name => "primary";
    public override string Usage => "<n>";
    public override string Description => "Make display n the main display";
    protected override int ExtraArgumentCount => 0;

    protected override string Execute(DisplayInfo display, IReadOnlyList<string> args)
    {
        layout.MakePrimary(display.Id);
        return $"Display {display.Number} is now the main display.";
    }
}

internal sealed class ProjectCommand(IProjectionSwitcher projection) : ICliCommand
{
    public string Name => "project";
    public string Usage => "<extend|duplicate|pconly|secondonly>";
    public string Description => "Same as Win+P";

    public int Execute(IReadOnlyList<string> args, TextWriter output)
    {
        var mode = args.Count == 1 ? args[0].ToLowerInvariant() : "";
        projection.SetProjection(mode switch
        {
            "extend" => ProjectionMode.Extend,
            "duplicate" => ProjectionMode.Duplicate,
            "pconly" => ProjectionMode.PcScreenOnly,
            "secondonly" => ProjectionMode.SecondScreenOnly,
            _ => throw new UsageException(),
        });
        output.WriteLine($"Projection set to {mode}.");
        return 0;
    }
}
