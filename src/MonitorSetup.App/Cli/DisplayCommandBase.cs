using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;

namespace MonitorSetup.App.Cli;

/// <summary>Base for commands whose first argument is a display number from <c>MonitorSetup list</c>.</summary>
internal abstract class DisplayCommandBase(IDisplayReader reader) : ICliCommand
{
    public abstract string Name { get; }
    public abstract string Usage { get; }
    public abstract string Description { get; }

    /// <summary>Number of arguments after the display number.</summary>
    protected abstract int ExtraArgumentCount { get; }

    public int Execute(IReadOnlyList<string> args, TextWriter output)
    {
        if (args.Count != ExtraArgumentCount + 1 || !int.TryParse(args[0], out int number))
            throw new UsageException();

        var display = reader.GetDisplays().FirstOrDefault(d => d.Number == number)
            ?? throw new DisplayException($"There is no display {number}. Run 'MonitorSetup list' to see the numbers.");

        output.WriteLine(Execute(display, args.Skip(1).ToList()));
        return 0;
    }

    /// <returns>Message describing what was done.</returns>
    protected abstract string Execute(DisplayInfo display, IReadOnlyList<string> args);

    protected static int ParseInt(string value) => int.TryParse(value, out int result) ? result : throw new UsageException();
}
