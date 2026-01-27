using StreamScanner.Maui.Models;

namespace StreamScanner.Maui.Services;

public interface IYouTubeService
{
    Task<List<LiveStream>> SearchLiveStreamsAsync(string keywords, int maxResults, CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
}
