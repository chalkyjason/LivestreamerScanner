using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamScanner.Maui.Models;
using StreamScanner.Maui.Services;

namespace StreamScanner.Maui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IYouTubeService _youTubeService;
    private CancellationTokenSource? _autoRefreshCts;
    private bool _isAutoRefreshing;

    [ObservableProperty]
    private string _keywords = "gaming, music, news";

    [ObservableProperty]
    private int _selectedMaxResults = 25;

    [ObservableProperty]
    private int _selectedRefreshInterval = 20;

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

    public ObservableCollection<LiveStream> Streams { get; } = new();

    public List<int> MaxResultsOptions { get; } = new() { 10, 25, 50 };
    public List<RefreshOption> RefreshOptions { get; } = new()
    {
        new RefreshOption(0, "Off"),
        new RefreshOption(10, "10s"),
        new RefreshOption(20, "20s"),
        new RefreshOption(30, "30s"),
        new RefreshOption(60, "60s")
    };

    public MainViewModel(IYouTubeService youTubeService)
    {
        _youTubeService = youTubeService;
    }

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
            SetStatus("API key not configured - check appsettings.json", "error");
            return;
        }

        IsScanning = true;
        SetStatus("Scanning...", "scanning");

        try
        {
            var results = await _youTubeService.SearchLiveStreamsAsync(Keywords, SelectedMaxResults);

            Streams.Clear();
            foreach (var stream in results)
            {
                Streams.Add(stream);
            }

            HasResults = Streams.Count > 0;
            ShowEmptyState = false;
            ShowNoResults = Streams.Count == 0;

            var time = DateTime.Now.ToString("HH:mm");
            if (Streams.Count > 0)
            {
                SetStatus($"{Streams.Count} live streams found - Updated {time}", "success");
            }
            else
            {
                SetStatus($"No results - Updated {time}", "default");
            }

            // Start auto-refresh if enabled
            StartAutoRefresh();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", "error");
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task OpenStreamAsync(LiveStream? stream)
    {
        if (stream == null) return;

        try
        {
            await Launcher.OpenAsync(new Uri(stream.Url));
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open URL: {ex.Message}", "error");
        }
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
