using StreamScanner.Maui.Views;

namespace StreamScanner.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }
}
