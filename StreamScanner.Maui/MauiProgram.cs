using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using StreamScanner.Maui.Converters;
using StreamScanner.Maui.Services;
using StreamScanner.Maui.ViewModels;
using StreamScanner.Maui.Views;

namespace StreamScanner.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Load API key from preferences or use default
        var apiKey = Preferences.Get("YouTubeApiKey", "YOUR_API_KEY_HERE");

        // Register services
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<IYouTubeService>(sp =>
            new YouTubeService(sp.GetRequiredService<HttpClient>(), apiKey));

        // Register ViewModels
        builder.Services.AddSingleton<MainViewModel>();

        // Register Views
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
