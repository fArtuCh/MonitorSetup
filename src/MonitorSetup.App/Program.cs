using Microsoft.Extensions.DependencyInjection;
using MonitorSetup.App.Cli;
using MonitorSetup.App.UI;
using MonitorSetup.Infrastructure.Windows;

namespace MonitorSetup.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        using var services = new ServiceCollection()
            .AddWindowsDisplayServices()
            .AddCli()
            .AddUserInterface()
            .BuildServiceProvider();

        if (args.Length > 0)
            return services.GetRequiredService<CliApplication>().Run(args);

        ApplicationConfiguration.Initialize();
        var presenter = services.GetRequiredService<MainPresenter>();
        presenter.Start();
        Application.Run(services.GetRequiredService<MainForm>());
        return 0;
    }
}
