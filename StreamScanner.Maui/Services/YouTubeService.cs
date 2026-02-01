using System.Net.Http.Json;
using StreamScanner.Maui.Models;

namespace StreamScanner.Maui.Services;

public class YouTubeService : IYouTubeService
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cacheService;
    private readonly string _apiKey;
    private const string BaseUrl = "https://www.googleapis.com/youtube/v3";

    private static readonly Dictionary<string, int> QuotaCosts = new()
    {
        ["search"] = 100,
        ["videos"] = 1,
        ["channels"] = 1
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && _apiKey != "YOUR_API_KEY_HERE";
    public int QuotaUsed { get; private set; }

    public YouTubeService(HttpClient httpClient, ICacheService cacheService, string apiKey)
    {
        _httpClient = httpClient;
        _cacheService = cacheService;
        _apiKey = apiKey;
    }

    public Task<List<LiveStream>> SearchLiveStreamsAsync(string keywords, int maxResults, CancellationToken cancellationToken = default)
    {
        return SearchLiveStreamsAsync(keywords, maxResults, null, cancellationToken);
    }

    public async Task<List<LiveStream>> SearchLiveStreamsAsync(string keywords, int maxResults, SearchFilters? filters, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("YouTube API key is not configured. Please add your API key in Settings.");
        }

        var eventType = filters?.EventType ?? "live";
        var order = filters?.SortOrder ?? "viewCount";
        var safeSearch = filters?.SafeSearch ?? "none";

        // Build optimized query
        var query = BuildOptimizedQuery(keywords);
        var blockedKey = filters?.BlockedChannels != null
            ? string.Join(",", filters.BlockedChannels.OrderBy(b => b))
            : "";
        var cacheKey = $"search:{query}:{maxResults}:{filters?.MinViewers ?? 0}:{filters?.MaxViewers ?? 0}:{blockedKey}:{filters?.Region}:{filters?.Language}:{filters?.TopicId}:{order}:{safeSearch}:{eventType}";

        // Check cache first
        var cached = await _cacheService.GetCachedSearchAsync(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        // Check rate limiting
        if (_cacheService.ShouldThrottle())
        {
            throw new InvalidOperationException("Rate limit reached. Please wait a moment before searching again.");
        }

        // Fetch extra results to account for filtering
        var blockedCount = filters?.BlockedChannels?.Count ?? 0;
        var fetchMax = Math.Min(maxResults + blockedCount * 2 + 10, 50);

        // Build search URL with all params
        var searchUrl = $"{BaseUrl}/search" +
            $"?part=snippet" +
            $"&type=video" +
            $"&eventType={Uri.EscapeDataString(eventType)}" +
            $"&maxResults={fetchMax}" +
            $"&order={Uri.EscapeDataString(order)}" +
            $"&safeSearch={Uri.EscapeDataString(safeSearch)}" +
            $"&q={Uri.EscapeDataString(query)}" +
            $"&key={Uri.EscapeDataString(_apiKey)}";

        if (!string.IsNullOrEmpty(filters?.Region))
            searchUrl += $"&regionCode={Uri.EscapeDataString(filters.Region)}";
        if (!string.IsNullOrEmpty(filters?.Language))
            searchUrl += $"&relevanceLanguage={Uri.EscapeDataString(filters.Language)}";
        if (!string.IsNullOrEmpty(filters?.TopicId))
            searchUrl += $"&topicId={Uri.EscapeDataString(filters.TopicId)}";

        _cacheService.RecordApiCall();
        QuotaUsed += QuotaCosts["search"];
        var searchResponse = await _httpClient.GetFromJsonAsync<YouTubeSearchResponse>(searchUrl, cancellationToken);

        if (searchResponse?.Items == null || searchResponse.Items.Count == 0)
        {
            await _cacheService.SetCachedSearchAsync(cacheKey, new List<LiveStream>(), TimeSpan.FromSeconds(15));
            return new List<LiveStream>();
        }

        // Extract video IDs
        var videoIds = searchResponse.Items
            .Where(item => item.Id?.VideoId != null)
            .Select(item => item.Id!.VideoId!)
            .ToList();

        if (videoIds.Count == 0)
        {
            return new List<LiveStream>();
        }

        // Get video details including live streaming details
        var videosUrl = $"{BaseUrl}/videos" +
            $"?part=snippet,liveStreamingDetails" +
            $"&id={Uri.EscapeDataString(string.Join(",", videoIds))}" +
            $"&key={Uri.EscapeDataString(_apiKey)}";

        _cacheService.RecordApiCall();
        QuotaUsed += QuotaCosts["videos"];
        var videosResponse = await _httpClient.GetFromJsonAsync<YouTubeVideosResponse>(videosUrl, cancellationToken);

        if (videosResponse?.Items == null)
        {
            return new List<LiveStream>();
        }

        var isUpcoming = eventType == "upcoming";

        // Map to LiveStream model
        var streams = videosResponse.Items.Select(video =>
        {
            int? viewers = null;
            if (int.TryParse(video.LiveStreamingDetails?.ConcurrentViewers, out var viewerCount))
            {
                viewers = viewerCount;
            }

            DateTime? scheduledStart = null;
            if (DateTime.TryParse(video.LiveStreamingDetails?.ScheduledStartTime, out var parsed))
            {
                scheduledStart = parsed.ToUniversalTime();
            }

            return new LiveStream
            {
                VideoId = video.Id,
                Title = video.Snippet?.Title ?? string.Empty,
                Description = video.Snippet?.Description ?? string.Empty,
                ChannelTitle = video.Snippet?.ChannelTitle ?? string.Empty,
                ChannelId = video.Snippet?.ChannelId ?? string.Empty,
                ThumbnailUrl = video.Snippet?.Thumbnails?.Medium?.Url
                    ?? video.Snippet?.Thumbnails?.Default?.Url
                    ?? string.Empty,
                ActualStartTime = video.LiveStreamingDetails?.ActualStartTime,
                ScheduledStartTime = scheduledStart,
                ConcurrentViewers = viewers,
                IsUpcoming = isUpcoming
            };
        }).ToList();

        // Apply filters
        if (filters != null)
        {
            // Remove blocked channels
            if (filters.BlockedChannels.Count > 0)
            {
                streams.RemoveAll(s =>
                    filters.BlockedChannels.Any(b =>
                        string.Equals(b, s.ChannelTitle, StringComparison.OrdinalIgnoreCase)));
            }

            // Apply viewer count filters (only for live, not upcoming)
            if (eventType == "live")
            {
                if (filters.MinViewers > 0)
                {
                    streams.RemoveAll(s =>
                        !s.ConcurrentViewers.HasValue || s.ConcurrentViewers.Value < filters.MinViewers);
                }

                if (filters.MaxViewers > 0)
                {
                    streams.RemoveAll(s =>
                        !s.ConcurrentViewers.HasValue || s.ConcurrentViewers.Value > filters.MaxViewers);
                }
            }
        }

        // Sort
        if (isUpcoming)
        {
            // Sort upcoming by scheduled start time (soonest first)
            streams.Sort((a, b) =>
            {
                var at = a.ScheduledStartTime ?? DateTime.MaxValue;
                var bt = b.ScheduledStartTime ?? DateTime.MaxValue;
                return at.CompareTo(bt);
            });
        }
        else
        {
            // Sort live by viewer count (descending), nulls last
            streams.Sort((a, b) =>
            {
                var av = a.ConcurrentViewers ?? -1;
                var bv = b.ConcurrentViewers ?? -1;
                return bv.CompareTo(av);
            });
        }

        // Trim to requested max
        if (streams.Count > maxResults)
        {
            streams = streams.Take(maxResults).ToList();
        }

        // Cache results
        await _cacheService.SetCachedSearchAsync(cacheKey, streams, TimeSpan.FromSeconds(30));

        return streams;
    }

    private static string BuildOptimizedQuery(string keywords)
    {
        var keywordList = keywords
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToList();

        if (keywordList.Count == 0)
            return keywords;

        if (keywordList.Count == 1)
            return keywordList[0];

        var formattedKeywords = keywordList.Select(k =>
        {
            if (k.Contains(' '))
                return $"\"{k}\"";
            return k;
        });

        return string.Join(" | ", formattedKeywords);
    }
}
