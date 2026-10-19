namespace MonitorSetup.App.Cli;

/// <summary>One verb of the command line, e.g. <c>MonitorSetup disable 3</c>.</summary>
internal interface ICliCommand
{
    string Name { get; }

    /// <summary>Arguments after the verb, shown in the usage text.</summary>
    string Usage { get; }

    string Description { get; }

    /// <returns>Process exit code.</returns>
    int Execute(IReadOnlyList<string> args, TextWriter output);
}
