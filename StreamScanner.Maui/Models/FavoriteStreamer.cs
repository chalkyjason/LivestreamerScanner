namespace StreamScanner.Maui.Models;

public class FavoriteStreamer
{
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Live status (populated when checking)
    public bool? IsLive { get; set; }
    public string? CurrentStreamTitle { get; set; }
    public string? CurrentStreamUrl { get; set; }
    public string? CurrentThumbnailUrl { get; set; }
    public int? CurrentViewers { get; set; }
    public DateTime? LastChecked { get; set; }

    public string LiveStatusDisplay => IsLive switch
    {
        true => "LIVE",
        false => "Offline",
        null => "Unknown"
    };

    public string ViewerCountDisplay => CurrentViewers.HasValue
        ? CurrentViewers.Value.ToString("N0")
        : "—";
}
