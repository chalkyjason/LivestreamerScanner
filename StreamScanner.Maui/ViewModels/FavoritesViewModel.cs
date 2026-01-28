using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamScanner.Maui.Models;
using StreamScanner.Maui.Services;

namespace StreamScanner.Maui.ViewModels;

public partial class FavoritesViewModel : ObservableObject
{
    private readonly IFavoritesService _favoritesService;
    private readonly IYouTubeService _youTubeService;
    private CancellationTokenSource? _autoRefreshCts;
    private bool _isAutoRefreshing;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private string _statusMessage = "Tap refresh to check live status";

    [ObservableProperty]
    private bool _hasFavorites;

    [ObservableProperty]
    private bool _showEmptyState = true;

    [ObservableProperty]
    private int _liveCount;

    [ObservableProperty]
    private int _selectedRefreshInterval = 0;

    public ObservableCollection<FavoriteStreamer> Favorites { get; } = new();

    public List<RefreshOption> RefreshOptions { get; } = new()
    {
        new RefreshOption(0, "Off"),
        new RefreshOption(30, "30s"),
        new RefreshOption(60, "60s"),
        new RefreshOption(120, "2m"),
        new RefreshOption(300, "5m")
    };

    public FavoritesViewModel(IFavoritesService favoritesService, IYouTubeService youTubeService)
    {
        _favoritesService = favoritesService;
        _youTubeService = youTubeService;
    }

    [RelayCommand]
    private void LoadFavorites()
    {
        var favorites = _favoritesService.GetFavorites();

        Favorites.Clear();
        foreach (var fav in favorites.OrderBy(f => f.ChannelTitle))
        {
            Favorites.Add(fav);
        }

        HasFavorites = Favorites.Count > 0;
        ShowEmptyState = Favorites.Count == 0;
        UpdateLiveCount();
    }

    [RelayCommand]
    private async Task CheckLiveStatusAsync()
    {
        if (!_youTubeService.IsConfigured)
        {
            StatusMessage = "API key not configured";
            return;
        }

        if (Favorites.Count == 0)
        {
            StatusMessage = "No favorites to check";
            return;
        }

        IsChecking = true;
        StatusMessage = "Checking live status...";

        try
        {
            var updatedFavorites = await _favoritesService.CheckFavoritesLiveStatusAsync();

            // Update the observable collection
            Favorites.Clear();
            foreach (var fav in updatedFavorites.OrderByDescending(f => f.IsLive == true).ThenBy(f => f.ChannelTitle))
            {
                Favorites.Add(fav);
            }

            UpdateLiveCount();

            var time = DateTime.Now.ToString("HH:mm");
            StatusMessage = LiveCount > 0
                ? $"{LiveCount} favorite(s) live - Updated {time}"
                : $"No favorites live - Updated {time}";

            // Start auto-refresh if enabled
            StartAutoRefresh();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private async Task OpenStreamAsync(FavoriteStreamer? favorite)
    {
        if (favorite?.CurrentStreamUrl == null) return;

        try
        {
            await Launcher.OpenAsync(new Uri(favorite.CurrentStreamUrl));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open URL: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveFavorite(FavoriteStreamer? favorite)
    {
        if (favorite == null) return;

        _favoritesService.RemoveFavorite(favorite.ChannelId);

        Favorites.Remove(favorite);
        HasFavorites = Favorites.Count > 0;
        ShowEmptyState = Favorites.Count == 0;
        UpdateLiveCount();

        StatusMessage = $"Removed {favorite.ChannelTitle} from favorites";
    }

    [RelayCommand]
    private void StopAutoRefresh()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts = null;
        _isAutoRefreshing = false;
    }

    partial void OnSelectedRefreshIntervalChanged(int value)
    {
        if (_isAutoRefreshing)
        {
            StopAutoRefresh();
            if (value > 0)
            {
                StartAutoRefresh();
            }
        }
    }

    private void StartAutoRefresh()
    {
        StopAutoRefresh();

        if (SelectedRefreshInterval <= 0) return;

        _isAutoRefreshing = true;
        _autoRefreshCts = new CancellationTokenSource();
        var token = _autoRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(SelectedRefreshInterval * 1000, token);

                if (token.IsCancellationRequested) break;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!IsChecking)
                    {
                        await CheckLiveStatusAsync();
                    }
                });
            }
        }, token);
    }

    private void UpdateLiveCount()
    {
        LiveCount = Favorites.Count(f => f.IsLive == true);
    }
}
