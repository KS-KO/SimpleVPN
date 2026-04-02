using SimpleVPN.Mobile.Pages;
using SimpleVPN.Mobile.Services;
using SimpleVPN.Mobile.ViewModels;

namespace SimpleVPN.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

#if ANDROID
        builder.Services.AddSingleton<IMobileVpnPlatformService, AndroidMobileVpnPlatformService>();
#else
        builder.Services.AddSingleton<IMobileVpnPlatformService, StubMobileVpnPlatformService>();
#endif
        builder.Services.AddSingleton<MobileServerCatalogService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
