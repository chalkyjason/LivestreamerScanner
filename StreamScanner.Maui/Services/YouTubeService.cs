using System.Net.Http.Json;
using StreamScanner.Maui.Models;

namespace StreamScanner.Maui.Services;

public class YouTubeService : IYouTubeService
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cacheService;
    private readonly string _apiKey;
    private const string BaseUrl = "https://www.googleapis.com/youtube/v3";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && _apiKey != "YOUR_API_KEY_HERE";

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

        // Build optimized query
        var query = BuildOptimizedQuery(keywords);
        var blockedKey = filters?.BlockedChannels != null
            ? string.Join(",", filters.BlockedChannels.OrderBy(b => b))
            : "";
        var cacheKey = $"search:{query}:{maxResults}:{filters?.MinViewers ?? 0}:{filters?.MaxViewers ?? 0}:{blockedKey}";

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

        // Step 1: Search for live videos with optimized parameters
        var searchUrl = $"{BaseUrl}/search" +
            $"?part=snippet" +
            $"&type=video" +
            $"&eventType=live" +
            $"&maxResults={fetchMax}" +
            $"&order=viewCount" +
            $"&safeSearch=none" +
            $"&q={Uri.EscapeDataString(query)}" +
            $"&key={Uri.EscapeDataString(_apiKey)}";

        _cacheService.RecordApiCall();
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

        // Step 2: Get video details including live streaming details
        var videosUrl = $"{BaseUrl}/videos" +
            $"?part=snippet,liveStreamingDetails" +
            $"&id={Uri.EscapeDataString(string.Join(",", videoIds))}" +
            $"&key={Uri.EscapeDataString(_apiKey)}";

        _cacheService.RecordApiCall();
        var videosResponse = await _httpClient.GetFromJsonAsync<YouTubeVideosResponse>(videosUrl, cancellationToken);

        if (videosResponse?.Items == null)
        {
            return new List<LiveStream>();
        }

        // Map to LiveStream model
        var streams = videosResponse.Items.Select(video =>
        {
            int? viewers = null;
            if (int.TryParse(video.LiveStreamingDetails?.ConcurrentViewers, out var viewerCount))
            {
                viewers = viewerCount;
            }

            return new LiveStream
            {
                VideoId = video.Id,
                Title = video.Snippet?.Title ?? string.Empty,
                ChannelTitle = video.Snippet?.ChannelTitle ?? string.Empty,
                ChannelId = video.Snippet?.ChannelId ?? string.Empty,
                ThumbnailUrl = video.Snippet?.Thumbnails?.Medium?.Url
                    ?? video.Snippet?.Thumbnails?.Default?.Url
                    ?? string.Empty,
                ActualStartTime = video.LiveStreamingDetails?.ActualStartTime,
                ConcurrentViewers = viewers
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

            // Apply min viewer filter
            if (filters.MinViewers > 0)
            {
                streams.RemoveAll(s =>
                    !s.ConcurrentViewers.HasValue || s.ConcurrentViewers.Value < filters.MinViewers);
            }

            // Apply max viewer filter
            if (filters.MaxViewers > 0)
            {
                streams.RemoveAll(s =>
                    !s.ConcurrentViewers.HasValue || s.ConcurrentViewers.Value > filters.MaxViewers);
            }
        }

        // Sort by viewer count (descending), nulls last
        streams.Sort((a, b) =>
        {
            var av = a.ConcurrentViewers ?? -1;
            var bv = b.ConcurrentViewers ?? -1;
            return bv.CompareTo(av);
        });

        // Trim to requested max
        if (streams.Count > maxResults)
        {
            streams = streams.Take(maxResults).ToList();
        }

        // Cache results
        await _cacheService.SetCachedSearchAsync(cacheKey, streams, TimeSpan.FromSeconds(30));

        return streams;
    }

    /// <summary>
    /// Build an optimized YouTube search query using search operators
    /// </summary>
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

        // Use OR operator for multiple keywords
        // Quote multi-word phrases for exact matching
        var formattedKeywords = keywordList.Select(k =>
        {
            // If keyword contains spaces, wrap in quotes for exact phrase matching
            if (k.Contains(' '))
                return $"\"{k}\"";
            return k;
        });

        return string.Join(" | ", formattedKeywords); // | is OR in YouTube search
    }
}
