using System.Text.Json.Serialization;

namespace StreamScanner.Maui.Models;

public class YouTubeSearchResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeSearchItem> Items { get; set; } = new();
}

public class YouTubeSearchItem
{
    [JsonPropertyName("id")]
    public YouTubeVideoId? Id { get; set; }

    [JsonPropertyName("snippet")]
    public YouTubeSnippet? Snippet { get; set; }
}

public class YouTubeVideoId
{
    [JsonPropertyName("videoId")]
    public string? VideoId { get; set; }
}

public class YouTubeVideosResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeVideoItem> Items { get; set; } = new();
}

public class YouTubeVideoItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("snippet")]
    public YouTubeSnippet? Snippet { get; set; }

    [JsonPropertyName("liveStreamingDetails")]
    public YouTubeLiveStreamingDetails? LiveStreamingDetails { get; set; }
}

public class YouTubeSnippet
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("channelTitle")]
    public string ChannelTitle { get; set; } = string.Empty;

    [JsonPropertyName("channelId")]
    public string ChannelId { get; set; } = string.Empty;

    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("thumbnails")]
    public YouTubeThumbnails? Thumbnails { get; set; }
}

public class YouTubeThumbnails
{
    [JsonPropertyName("default")]
    public YouTubeThumbnail? Default { get; set; }

    [JsonPropertyName("medium")]
    public YouTubeThumbnail? Medium { get; set; }

    [JsonPropertyName("high")]
    public YouTubeThumbnail? High { get; set; }
}

public class YouTubeThumbnail
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public class YouTubeLiveStreamingDetails
{
    [JsonPropertyName("actualStartTime")]
    public DateTime? ActualStartTime { get; set; }

    [JsonPropertyName("scheduledStartTime")]
    public string? ScheduledStartTime { get; set; }

    [JsonPropertyName("concurrentViewers")]
    public string? ConcurrentViewers { get; set; }
}
