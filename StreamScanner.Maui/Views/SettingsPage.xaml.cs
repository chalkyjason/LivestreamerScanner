namespace StreamScanner.Maui.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();

        // Load existing API key
        var existingKey = Preferences.Get("YouTubeApiKey", "");
        if (!string.IsNullOrEmpty(existingKey) && existingKey != "YOUR_API_KEY_HERE")
        {
            ApiKeyEntry.Text = existingKey;
        }
    }

    private async void OnSaveApiKeyClicked(object? sender, EventArgs e)
    {
        var apiKey = ApiKeyEntry.Text?.Trim();

        if (string.IsNullOrEmpty(apiKey))
        {
            await DisplayAlert("Error", "Please enter an API key", "OK");
            return;
        }

        Preferences.Set("YouTubeApiKey", apiKey);

        await DisplayAlert("Success", "API key saved! Please restart the app for changes to take effect.", "OK");
    }

    private async void OnOpenConsoleClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.OpenAsync(new Uri("https://console.cloud.google.com/apis/credentials"));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not open browser: {ex.Message}", "OK");
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
