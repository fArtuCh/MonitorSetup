using Microsoft.Extensions.DependencyInjection;
using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Services;
using MonitorSetup.Infrastructure.Windows.Persistence;
using MonitorSetup.Infrastructure.Windows.Services;

namespace MonitorSetup.Infrastructure.Windows;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the Win32 display implementations and the Core services built on them.</summary>
    public static IServiceCollection AddWindowsDisplayServices(this IServiceCollection services, string? stateFilePath = null)
    {
        services.AddSingleton<IDisplayReader, WindowsDisplayReader>();
        services.AddSingleton<IDisplayTopology, WindowsDisplayTopology>();
        services.AddSingleton<IProjectionSwitcher, WindowsProjectionSwitcher>();
        services.AddSingleton<IDisplayModeSetter, WindowsDisplayModeSetter>();
        services.AddSingleton<IDisplayPositioner, WindowsDisplayPositioner>();
        services.AddSingleton<IDisplayScaleSetter, WindowsDisplayScaleSetter>();
        services.AddSingleton<IHdrSetter, WindowsHdrSetter>();
        services.AddSingleton<IDisplayConfigurationSnapshots, WindowsDisplaySnapshots>();
        services.AddSingleton<IDisplayStateStore>(_ => new JsonDisplayStateStore(stateFilePath ?? JsonDisplayStateStore.DefaultPath));

        services.AddSingleton<IDisplayLayoutService, DisplayLayoutService>();
        services.AddSingleton<IDisplayPowerService, DisplayPowerService>();
        return services;
    }
}
