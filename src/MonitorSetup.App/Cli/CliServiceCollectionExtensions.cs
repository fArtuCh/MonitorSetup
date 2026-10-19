using Microsoft.Extensions.DependencyInjection;
using MonitorSetup.App.Cli.Commands;

namespace MonitorSetup.App.Cli;

internal static class CliServiceCollectionExtensions
{
    public static IServiceCollection AddCli(this IServiceCollection services)
    {
        // Registration order is the order shown in the help text.
        services.AddSingleton<ICliCommand, ListCommand>();
        services.AddSingleton<ICliCommand, EnableCommand>();
        services.AddSingleton<ICliCommand, DisableCommand>();
        services.AddSingleton<ICliCommand, ToggleCommand>();
        services.AddSingleton<ICliCommand, PrimaryCommand>();
        services.AddSingleton<ICliCommand, ModeCommand>();
        services.AddSingleton<ICliCommand, RotateCommand>();
        services.AddSingleton<ICliCommand, ScaleCommand>();
        services.AddSingleton<ICliCommand, MoveCommand>();
        services.AddSingleton<ICliCommand, ProjectCommand>();
        services.AddSingleton<CliApplication>();
        return services;
    }
}
