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
}
