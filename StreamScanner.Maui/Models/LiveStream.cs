namespace StreamScanner.Maui.Models;

public class LiveStream
{
    public string VideoId { get; set; } = string.Empty;
    public string Url => $"https://www.youtube.com/watch?v={VideoId}";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public int? ConcurrentViewers { get; set; }
    public bool IsUpcoming { get; set; }

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

    public string ScheduledTimeDisplay
    {
        get
        {
            if (!ScheduledStartTime.HasValue)
                return "Not scheduled";

            var diff = ScheduledStartTime.Value - DateTime.UtcNow;

            if (diff.TotalMinutes < 0)
                return "Starting soon";
            if (diff.TotalMinutes < 60)
                return $"In {(int)diff.TotalMinutes}m";
            if (diff.TotalHours < 24)
                return $"In {(int)diff.TotalHours}h {diff.Minutes}m";
            if (diff.TotalDays < 7)
                return $"In {(int)diff.TotalDays}d {diff.Hours}h";

            return ScheduledStartTime.Value.ToLocalTime().ToString("MMM d, h:mm tt");
        }
    }

    public bool HasViewerCount => ConcurrentViewers.HasValue;
}
