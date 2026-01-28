using System.Text.Json;
using StreamScanner.Maui.Models;

namespace StreamScanner.Maui.Services;

public class FavoritesService : IFavoritesService
{
    private const string FavoritesKey = "FavoriteStreamers";
    private readonly IYouTubeService _youTubeService;
    private List<FavoriteStreamer>? _cachedFavorites;

    public FavoritesService(IYouTubeService youTubeService)
    {
        _youTubeService = youTubeService;
    }

    public List<FavoriteStreamer> GetFavorites()
    {
        if (_cachedFavorites != null)
            return _cachedFavorites;

        var json = Preferences.Get(FavoritesKey, "[]");
        try
        {
            _cachedFavorites = JsonSerializer.Deserialize<List<FavoriteStreamer>>(json) ?? new List<FavoriteStreamer>();
        }
        catch
        {
            _cachedFavorites = new List<FavoriteStreamer>();
        }

        return _cachedFavorites;
    }

    public void AddFavorite(string channelId, string channelTitle)
    {
        var favorites = GetFavorites();

        if (favorites.Any(f => f.ChannelId == channelId))
            return;

        favorites.Add(new FavoriteStreamer
        {
            ChannelId = channelId,
            ChannelTitle = channelTitle,
            AddedAt = DateTime.UtcNow
        });

        SaveFavorites(favorites);
    }

    public void RemoveFavorite(string channelId)
    {
        var favorites = GetFavorites();
        var toRemove = favorites.FirstOrDefault(f => f.ChannelId == channelId);

        if (toRemove != null)
        {
            favorites.Remove(toRemove);
            SaveFavorites(favorites);
        }
    }

    public bool IsFavorite(string channelId)
    {
        return GetFavorites().Any(f => f.ChannelId == channelId);
    }

    public async Task<List<FavoriteStreamer>> CheckFavoritesLiveStatusAsync(CancellationToken cancellationToken = default)
    {
        var favorites = GetFavorites();

        if (favorites.Count == 0 || !_youTubeService.IsConfigured)
            return favorites;

        // Search for each favorite's channel name to see if they're live
        foreach (var favorite in favorites)
        {
            try
            {
                // Search for live streams from this channel
                var liveStreams = await _youTubeService.SearchLiveStreamsAsync(
                    favorite.ChannelTitle,
                    10,
                    cancellationToken);

                // Find streams that match this channel exactly
                var channelStream = liveStreams.FirstOrDefault(s =>
                    s.ChannelTitle.Equals(favorite.ChannelTitle, StringComparison.OrdinalIgnoreCase));

                if (channelStream != null)
                {
                    favorite.IsLive = true;
                    favorite.CurrentStreamTitle = channelStream.Title;
                    favorite.CurrentStreamUrl = channelStream.Url;
                    favorite.CurrentThumbnailUrl = channelStream.ThumbnailUrl;
                    favorite.CurrentViewers = channelStream.ConcurrentViewers;
                }
                else
                {
                    favorite.IsLive = false;
                    favorite.CurrentStreamTitle = null;
                    favorite.CurrentStreamUrl = null;
                    favorite.CurrentThumbnailUrl = null;
                    favorite.CurrentViewers = null;
                }

                favorite.LastChecked = DateTime.UtcNow;
            }
            catch
            {
                // Keep previous status on error
                favorite.LastChecked = DateTime.UtcNow;
            }
        }

        return favorites;
    }

    private void SaveFavorites(List<FavoriteStreamer> favorites)
    {
        _cachedFavorites = favorites;
        var json = JsonSerializer.Serialize(favorites);
        Preferences.Set(FavoritesKey, json);
    }
}
