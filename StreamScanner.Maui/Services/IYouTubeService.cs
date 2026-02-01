using StreamScanner.Maui.Models;

namespace StreamScanner.Maui.Services;

public interface IYouTubeService
{
    Task<List<LiveStream>> SearchLiveStreamsAsync(string keywords, int maxResults, CancellationToken cancellationToken = default);
    Task<List<LiveStream>> SearchLiveStreamsAsync(string keywords, int maxResults, SearchFilters? filters, CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
    int QuotaUsed { get; }
}

public class SearchFilters
{
    public int MinViewers { get; set; }
    public int MaxViewers { get; set; }
    public List<string> BlockedChannels { get; set; } = new();
    public string Region { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string TopicId { get; set; } = string.Empty;
    public string SortOrder { get; set; } = "viewCount";
    public string SafeSearch { get; set; } = "none";
    public string EventType { get; set; } = "live";
}
