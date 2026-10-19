using Microsoft.Extensions.DependencyInjection;

namespace MonitorSetup.App.UI;

internal static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddUserInterface(this IServiceCollection services)
    {
        services.AddSingleton<MainForm>();
        services.AddSingleton<IMainView>(sp => sp.GetRequiredService<MainForm>());
        services.AddSingleton<MainPresenter>();
        services.AddSingleton<IChangeConfirmation, DialogChangeConfirmation>();
        services.AddSingleton<IIdentifyOverlay, IdentifyOverlay>();
        return services;
    }
}
