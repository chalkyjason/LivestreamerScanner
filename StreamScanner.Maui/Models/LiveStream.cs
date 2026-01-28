namespace StreamScanner.Maui.Models;

public class LiveStream
{
    public string VideoId { get; set; } = string.Empty;
    public string Url => $"https://www.youtube.com/watch?v={VideoId}";
    public string Title { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTime? ActualStartTime { get; set; }
    public int? ConcurrentViewers { get; set; }

    public string ViewerCountDisplay => ConcurrentViewers.HasValue
        ? ConcurrentViewers.Value.ToString("N0")
        : "—";

    public string StartTimeDisplay
    {
        get
        {
            if (!ActualStartTime.HasValue)
                return "Unknown";

            var diff = DateTime.UtcNow - ActualStartTime.Value;

            if (diff.TotalMinutes < 1)
                return "Just started";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours}h {diff.Minutes}m ago";

            return ActualStartTime.Value.ToLocalTime().ToString("g");
        }
    }

    public bool HasViewerCount => ConcurrentViewers.HasValue;
}
