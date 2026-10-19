using System.Runtime.InteropServices;
using MonitorSetup.Core.Models;

namespace MonitorSetup.App.Cli;

/// <summary>Dispatches command line arguments to the matching <see cref="ICliCommand"/>.</summary>
internal sealed class CliApplication(IEnumerable<ICliCommand> commands)
{
    private readonly IReadOnlyList<ICliCommand> _commands = commands.ToList();

    public int Run(IReadOnlyList<string> args)
    {
        // A WinExe has no console of its own; borrow the one we were started from.
        if (!Console.IsOutputRedirected)
            AttachConsole(AttachParentProcess);

        string verb = args[0].TrimStart('-').ToLowerInvariant();
        var command = _commands.FirstOrDefault(c => c.Name == verb);
        if (command is null)
        {
            PrintUsage(verb is "help" or "h" or "?" ? Console.Out : Console.Error);
            return verb is "help" or "h" or "?" ? 0 : 2;
        }

        try
        {
            return command.Execute(args.Skip(1).ToList(), Console.Out);
        }
        catch (UsageException)
        {
            Console.Error.WriteLine($"Usage: MonitorSetup {command.Name} {command.Usage}");
            return 2;
        }
        catch (DisplayException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private void PrintUsage(TextWriter output)
    {
        output.WriteLine("Usage: MonitorSetup [command]   (no command opens the window)");
        output.WriteLine();
        foreach (var c in _commands)
            output.WriteLine($"  {$"{c.Name} {c.Usage}",-36} {c.Description}");
    }

    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}

/// <summary>Thrown by a command when its arguments are wrong.</summary>
internal sealed class UsageException : Exception;
