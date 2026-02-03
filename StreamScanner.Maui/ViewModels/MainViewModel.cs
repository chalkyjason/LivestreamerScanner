using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamScanner.Maui.Models;
using StreamScanner.Maui.Services;

namespace StreamScanner.Maui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IYouTubeService _youTubeService;
    private readonly IFavoritesService _favoritesService;
    private readonly IBlockedChannelsService _blockedChannelsService;
    private CancellationTokenSource? _autoRefreshCts;
    private CancellationTokenSource? _scanCts;
    private bool _isAutoRefreshing;

    [ObservableProperty]
    private string _keywords = "gaming, music, news";

    [ObservableProperty]
    private int _selectedMaxResults = 25;

    [ObservableProperty]
    private int _selectedRefreshInterval = 20;

    [ObservableProperty]
    private int _minViewers;

    [ObservableProperty]
    private int _maxViewers;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "Ready to scan";

    [ObservableProperty]
    private string _statusType = "default";

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _showEmptyState = true;

    [ObservableProperty]
    private bool _showNoResults;

    [ObservableProperty]
    private bool _hasBlockedChannels;

    // New filter properties (bound to Picker SelectedItem as FilterOption objects)
    [ObservableProperty]
    private FilterOption? _selectedRegion;

    [ObservableProperty]
    private FilterOption? _selectedLanguage;

    [ObservableProperty]
    private FilterOption? _selectedTopic;

    [ObservableProperty]
    private FilterOption? _selectedSortOrder;

    [ObservableProperty]
    private FilterOption? _selectedSafeSearch;

    [ObservableProperty]
    private string _activeTab = "live";

    [ObservableProperty]
    private bool _isLiveTab = true;

    [ObservableProperty]
    private bool _isUpcomingTab;

    [ObservableProperty]
    private int _quotaUsed;

    [ObservableProperty]
    private double _quotaPercent;

    [ObservableProperty]
    private string _quotaDisplay = "0 / 10,000";

    // Presets
    [ObservableProperty]
    private string? _selectedPreset;

    public ObservableCollection<LiveStreamItem> Streams { get; } = new();
    public ObservableCollection<string> BlockedChannels { get; } = new();
    public ObservableCollection<string> PresetNames { get; } = new();

    public List<int> MaxResultsOptions { get; } = new() { 10, 25, 50 };
    public List<RefreshOption> RefreshOptions { get; } = new()
    {
        new RefreshOption(0, "Off"),
        new RefreshOption(10, "10s"),
        new RefreshOption(20, "20s"),
        new RefreshOption(30, "30s"),
        new RefreshOption(60, "60s")
    };

    public List<FilterOption> RegionOptions { get; } = new()
    {
        new FilterOption("", "Any"),
        new FilterOption("US", "US"),
        new FilterOption("GB", "UK"),
        new FilterOption("CA", "Canada"),
        new FilterOption("AU", "Australia"),
        new FilterOption("DE", "Germany"),
        new FilterOption("FR", "France"),
        new FilterOption("JP", "Japan"),
        new FilterOption("KR", "S. Korea"),
        new FilterOption("BR", "Brazil"),
        new FilterOption("IN", "India"),
        new FilterOption("MX", "Mexico"),
        new FilterOption("ES", "Spain"),
        new FilterOption("IT", "Italy"),
        new FilterOption("RU", "Russia"),
        new FilterOption("PH", "Philippines")
    };

    public List<FilterOption> LanguageOptions { get; } = new()
    {
        new FilterOption("", "Any"),
        new FilterOption("en", "English"),
        new FilterOption("es", "Spanish"),
        new FilterOption("pt", "Portuguese"),
        new FilterOption("fr", "French"),
        new FilterOption("de", "German"),
        new FilterOption("ja", "Japanese"),
        new FilterOption("ko", "Korean"),
        new FilterOption("hi", "Hindi"),
        new FilterOption("ru", "Russian"),
        new FilterOption("it", "Italian"),
        new FilterOption("zh", "Chinese"),
        new FilterOption("ar", "Arabic"),
        new FilterOption("tl", "Filipino")
    };

    public List<FilterOption> TopicOptions { get; } = new()
    {
        new FilterOption("", "Any"),
        new FilterOption("/m/0bzvm2", "Gaming"),
        new FilterOption("/m/04rlf", "Music"),
        new FilterOption("/m/06ntj", "Sports"),
        new FilterOption("/m/02jjt", "Entertainment"),
        new FilterOption("/m/019_rr", "Lifestyle"),
        new FilterOption("/m/01k8wb", "Knowledge"),
        new FilterOption("/m/098wr", "Society")
    };

    public List<FilterOption> SortOptions { get; } = new()
    {
        new FilterOption("viewCount", "Most Viewers"),
        new FilterOption("date", "Newest"),
        new FilterOption("relevance", "Relevance"),
        new FilterOption("rating", "Rating")
    };

    public List<FilterOption> SafeSearchOptions { get; } = new()
    {
        new FilterOption("none", "Off"),
        new FilterOption("moderate", "Moderate"),
        new FilterOption("strict", "Strict")
    };

    public MainViewModel(IYouTubeService youTubeService, IFavoritesService favoritesService, IBlockedChannelsService blockedChannelsService)
    {
        _youTubeService = youTubeService;
        _favoritesService = favoritesService;
        _blockedChannelsService = blockedChannelsService;

        // Set default filter selections
        _selectedSortOrder = SortOptions[0];      // viewCount
        _selectedRegion = RegionOptions[0];        // Any
        _selectedLanguage = LanguageOptions[0];    // Any
        _selectedTopic = TopicOptions[0];          // Any
        _selectedSafeSearch = SafeSearchOptions[0]; // none

        _blockedChannelsService.BlockListChanged += RefreshBlockedList;
        RefreshBlockedList();
        LoadPresetNames();
    }

    private void RefreshBlockedList()
    {
        BlockedChannels.Clear();
        foreach (var ch in _blockedChannelsService.GetBlockedChannels())
        {
            BlockedChannels.Add(ch);
        }
        HasBlockedChannels = BlockedChannels.Count > 0;
    }

    // ── Tabs ──

    [RelayCommand]
    private async Task SwitchToLive()
    {
        ActiveTab = "live";
        IsLiveTab = true;
        IsUpcomingTab = false;
        await ScanAsync();
    }

    [RelayCommand]
    private async Task SwitchToUpcoming()
    {
        ActiveTab = "upcoming";
        IsLiveTab = false;
        IsUpcomingTab = true;
        await ScanAsync();
    }

    // ── Presets ──

    private Dictionary<string, PresetData> GetPresets()
    {
        try
        {
            var json = Preferences.Get("searchPresets", "{}");
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PresetData>>(json)
                ?? new Dictionary<string, PresetData>();
        }
        catch
        {
            return new Dictionary<string, PresetData>();
        }
    }

    private void SavePresetsStore(Dictionary<string, PresetData> presets)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(presets);
        Preferences.Set("searchPresets", json);
        LoadPresetNames();
    }

    private void LoadPresetNames()
    {
        PresetNames.Clear();
        foreach (var name in GetPresets().Keys.OrderBy(k => k))
        {
            PresetNames.Add(name);
        }
    }

    [RelayCommand]
    private void SavePreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var presets = GetPresets();
        presets[name.Trim()] = new PresetData
        {
            Keywords = Keywords,
            MaxResults = SelectedMaxResults,
            MinViewers = MinViewers,
            MaxViewers = MaxViewers,
            SortOrder = SelectedSortOrder?.Value ?? "viewCount",
            Region = SelectedRegion?.Value ?? "",
            Language = SelectedLanguage?.Value ?? "",
            TopicId = SelectedTopic?.Value ?? "",
            SafeSearch = SelectedSafeSearch?.Value ?? "none",
            RefreshInterval = SelectedRefreshInterval
        };
        SavePresetsStore(presets);
        SetStatus($"Saved preset \"{name.Trim()}\"", "success");
    }

    [RelayCommand]
    private void DeletePreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var presets = GetPresets();
        if (presets.Remove(name))
        {
            SavePresetsStore(presets);
            SelectedPreset = null;
            SetStatus($"Deleted preset \"{name}\"", "default");
        }
    }

    [RelayCommand]
    private async Task LoadPreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var presets = GetPresets();
        if (!presets.TryGetValue(name, out var preset)) return;

        Keywords = preset.Keywords;
        SelectedMaxResults = preset.MaxResults;
        MinViewers = preset.MinViewers;
        MaxViewers = preset.MaxViewers;
        SelectedSortOrder = SortOptions.FirstOrDefault(o => o.Value == preset.SortOrder) ?? SortOptions[0];
        SelectedRegion = RegionOptions.FirstOrDefault(o => o.Value == preset.Region) ?? RegionOptions[0];
        SelectedLanguage = LanguageOptions.FirstOrDefault(o => o.Value == preset.Language) ?? LanguageOptions[0];
        SelectedTopic = TopicOptions.FirstOrDefault(o => o.Value == preset.TopicId) ?? TopicOptions[0];
        SelectedSafeSearch = SafeSearchOptions.FirstOrDefault(o => o.Value == preset.SafeSearch) ?? SafeSearchOptions[0];
        SelectedRefreshInterval = preset.RefreshInterval;

        await ScanAsync();
    }

    // ── Scanning ──

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(Keywords))
        {
            SetStatus("Enter at least 1 keyword", "error");
            return;
        }

        if (!_youTubeService.IsConfigured)
        {
            SetStatus("API key not configured - check Settings", "error");
            return;
        }

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        IsScanning = true;
        SetStatus("Scanning...", "scanning");

        try
        {
            var filters = new SearchFilters
            {
                MinViewers = MinViewers,
                MaxViewers = MaxViewers,
                BlockedChannels = _blockedChannelsService.GetBlockedChannels(),
                Region = SelectedRegion?.Value ?? "",
                Language = SelectedLanguage?.Value ?? "",
                TopicId = SelectedTopic?.Value ?? "",
                SortOrder = SelectedSortOrder?.Value ?? "viewCount",
                SafeSearch = SelectedSafeSearch?.Value ?? "none",
                EventType = ActiveTab == "upcoming" ? "upcoming" : "live"
            };

            var results = await _youTubeService.SearchLiveStreamsAsync(Keywords, SelectedMaxResults, filters, token);

            if (token.IsCancellationRequested)
            {
                SetStatus("Scan cancelled", "default");
                return;
            }

            Streams.Clear();
            foreach (var stream in results)
            {
                var item = new LiveStreamItem(stream, _favoritesService.IsFavorite(stream.ChannelId));
                Streams.Add(item);
            }

            HasResults = Streams.Count > 0;
            ShowEmptyState = false;
            ShowNoResults = Streams.Count == 0;

            // Update quota
            QuotaUsed = _youTubeService.QuotaUsed;
            QuotaPercent = Math.Min((double)QuotaUsed / 10000 * 100, 100);
            QuotaDisplay = $"{QuotaUsed:N0} / 10,000";

            var time = DateTime.Now.ToString("HH:mm");
            var label = ActiveTab == "upcoming" ? "upcoming streams" : "live streams";
            if (Streams.Count > 0)
            {
                SetStatus($"{Streams.Count} {label} found - Updated {time}", "success");
            }
            else
            {
                SetStatus($"No results - Updated {time}", "default");
            }

            StartAutoRefresh();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Scan cancelled", "default");
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                SetStatus($"Error: {ex.Message}", "error");
            }
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    [RelayCommand]
    private async Task OpenStreamAsync(LiveStreamItem? item)
    {
        if (item?.Stream == null) return;

        try
        {
            await Launcher.OpenAsync(new Uri(item.Stream.Url));
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open URL: {ex.Message}", "error");
        }
    }

    [RelayCommand]
    private async Task CopyLinkAsync(LiveStreamItem? item)
    {
        if (item?.Stream == null) return;

        try
        {
            await Clipboard.SetTextAsync(item.Stream.Url);
            SetStatus("Link copied!", "success");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not copy: {ex.Message}", "error");
        }
    }

    [RelayCommand]
    private void StopScan()
    {
        _scanCts?.Cancel();
        StopAutoRefresh();
        SetStatus("Scan stopped", "default");
    }

    [RelayCommand]
    private void ToggleFavorite(LiveStreamItem? item)
    {
        if (item?.Stream == null) return;

        var channelId = item.Stream.ChannelId;
        var channelTitle = item.Stream.ChannelTitle;

        if (item.IsFavorite)
        {
            _favoritesService.RemoveFavorite(channelId);
            item.IsFavorite = false;
            SetStatus($"Removed {channelTitle} from favorites", "default");
        }
        else
        {
            _favoritesService.AddFavorite(channelId, channelTitle);
            item.IsFavorite = true;
            SetStatus($"Added {channelTitle} to favorites", "success");
        }
    }

    [RelayCommand]
    private void BlockChannel(LiveStreamItem? item)
    {
        if (item?.Stream == null) return;

        var channelTitle = item.Stream.ChannelTitle;
        _blockedChannelsService.BlockChannel(channelTitle);

        var toRemove = Streams.Where(s =>
            string.Equals(s.Stream.ChannelTitle, channelTitle, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var s in toRemove)
        {
            Streams.Remove(s);
        }

        HasResults = Streams.Count > 0;
        ShowNoResults = !HasResults && !ShowEmptyState;
        SetStatus($"Blocked {channelTitle}", "default");
    }

    [RelayCommand]
    private void UnblockChannel(string? channelTitle)
    {
        if (string.IsNullOrEmpty(channelTitle)) return;
        _blockedChannelsService.UnblockChannel(channelTitle);
        SetStatus($"Unblocked {channelTitle}", "default");
    }

    [RelayCommand]
    private void ClearBlockedChannels()
    {
        _blockedChannelsService.ClearAll();
        SetStatus("Cleared all blocked channels", "default");
    }

    [RelayCommand]
    private void StopAutoRefresh()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts = null;
        _isAutoRefreshing = false;
    }

    partial void OnSelectedRefreshIntervalChanged(int value)
    {
        if (_isAutoRefreshing)
        {
            StopAutoRefresh();
            if (value > 0)
            {
                StartAutoRefresh();
            }
        }
    }

    private void StartAutoRefresh()
    {
        StopAutoRefresh();

        if (SelectedRefreshInterval <= 0) return;

        _isAutoRefreshing = true;
        _autoRefreshCts = new CancellationTokenSource();
        var token = _autoRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(SelectedRefreshInterval * 1000, token);

                if (token.IsCancellationRequested) break;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!IsScanning)
                    {
                        await ScanAsync();
                    }
                });
            }
        }, token);
    }

    private void SetStatus(string message, string type)
    {
        StatusMessage = message;
        StatusType = type;
    }
}

public record RefreshOption(int Seconds, string Display);
public record FilterOption(string Value, string Display);

public class PresetData
{
    public string Keywords { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 25;
    public int MinViewers { get; set; }
    public int MaxViewers { get; set; }
    public string SortOrder { get; set; } = "viewCount";
    public string Region { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string TopicId { get; set; } = string.Empty;
    public string SafeSearch { get; set; } = "none";
    public int RefreshInterval { get; set; } = 20;
}

public partial class LiveStreamItem : ObservableObject
{
    public LiveStream Stream { get; }

    [ObservableProperty]
    private bool _isFavorite;

    public string FavoriteIcon => IsFavorite ? "❤️" : "🤍";

    public LiveStreamItem(LiveStream stream, bool isFavorite)
    {
        Stream = stream;
        _isFavorite = isFavorite;
    }

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteIcon));
    }
}
