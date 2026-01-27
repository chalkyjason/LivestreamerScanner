using System.Net.Http.Json;
using StreamScanner.Maui.Models;

namespace StreamScanner.Maui.Services;

public class YouTubeService : IYouTubeService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "https://www.googleapis.com/youtube/v3";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && _apiKey != "YOUR_API_KEY_HERE";

    public YouTubeService(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<List<LiveStream>> SearchLiveStreamsAsync(string keywords, int maxResults, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("YouTube API key is not configured. Please add your API key to appsettings.json");
        }

        // Build query string with OR for multiple keywords
        var keywordList = keywords
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var query = keywordList.Count == 1
            ? keywordList[0]
            : string.Join(" OR ", keywordList.Select(k => $"\"{k}\""));

        // Step 1: Search for live videos
        var searchUrl = $"{BaseUrl}/search" +
            $"?part=snippet" +
            $"&type=video" +
            $"&eventType=live" +
            $"&maxResults={maxResults}" +
            $"&q={Uri.EscapeDataString(query)}" +
            $"&key={Uri.EscapeDataString(_apiKey)}";

        var searchResponse = await _httpClient.GetFromJsonAsync<YouTubeSearchResponse>(searchUrl, cancellationToken);

        if (searchResponse?.Items == null || searchResponse.Items.Count == 0)
        {
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
                ThumbnailUrl = video.Snippet?.Thumbnails?.Medium?.Url
                    ?? video.Snippet?.Thumbnails?.Default?.Url
                    ?? string.Empty,
                ActualStartTime = video.LiveStreamingDetails?.ActualStartTime,
                ConcurrentViewers = viewers
            };
        }).ToList();

        // Sort by viewer count (descending), nulls last
        streams.Sort((a, b) =>
        {
            var av = a.ConcurrentViewers ?? -1;
            var bv = b.ConcurrentViewers ?? -1;
            return bv.CompareTo(av);
        });

        return streams;
    }
}
