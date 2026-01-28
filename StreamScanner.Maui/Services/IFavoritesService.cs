using StreamScanner.Maui.Models;

namespace StreamScanner.Maui.Services;

public interface IFavoritesService
{
    List<FavoriteStreamer> GetFavorites();
    void AddFavorite(string channelId, string channelTitle);
    void RemoveFavorite(string channelId);
    bool IsFavorite(string channelId);
    Task<List<FavoriteStreamer>> CheckFavoritesLiveStatusAsync(CancellationToken cancellationToken = default);
}
