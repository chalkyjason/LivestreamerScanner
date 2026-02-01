using StreamScanner.Maui.ViewModels;

namespace StreamScanner.Maui.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }

    private async void OnFavoritesTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(FavoritesPage));
    }

    private async void OnSavePresetClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Save Preset", "Enter a name for this preset:", "Save", "Cancel", "Preset name");
        if (!string.IsNullOrWhiteSpace(name) && BindingContext is MainViewModel vm)
        {
            vm.SavePresetCommand.Execute(name);
        }
    }
}
