using System.Collections.Concurrent;
using StreamScanner.Maui.Models;

namespace StreamScanner.Maui.Services;

/// <summary>
/// Smart caching service to reduce YouTube API calls.
/// - Caches search results with TTL
/// - Tracks API usage to avoid quota limits
/// - Deduplicates concurrent requests
/// </summary>
public interface ICacheService
{
    Task<List<LiveStream>?> GetCachedSearchAsync(string cacheKey);
    Task SetCachedSearchAsync(string cacheKey, List<LiveStream> results, TimeSpan? ttl = null);
    void InvalidateCache(string? pattern = null);
    CacheStats GetStats();
    bool ShouldThrottle();
    void RecordApiCall();
}

public class CacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry<List<LiveStream>>> _searchCache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _requestLocks = new();

    private int _apiCallsThisMinute = 0;
    private DateTime _minuteStart = DateTime.UtcNow;
    private readonly object _rateLimitLock = new();

    // YouTube API has 10,000 units/day quota
    // Search costs 100 units, videos.list costs 1 unit per video
    // Conservative: ~50 searches per hour = 5000 units for search + 5000 for details
    private const int MaxCallsPerMinute = 10;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxTtl = TimeSpan.FromMinutes(5);

    public async Task<List<LiveStream>?> GetCachedSearchAsync(string cacheKey)
    {
        // Check if we have a valid cached result
        if (_searchCache.TryGetValue(cacheKey, out var entry))
        {
            if (!entry.IsExpired)
            {
                entry.Hits++;
                return entry.Value;
            }

            // Expired, remove it
            _searchCache.TryRemove(cacheKey, out _);
        }

        return null;
    }

    public Task SetCachedSearchAsync(string cacheKey, List<LiveStream> results, TimeSpan? ttl = null)
    {
        var actualTtl = ttl ?? DefaultTtl;
        if (actualTtl > MaxTtl) actualTtl = MaxTtl;

        var entry = new CacheEntry<List<LiveStream>>(results, actualTtl);
        _searchCache[cacheKey] = entry;

        // Cleanup old entries periodically
        if (_searchCache.Count > 100)
        {
            CleanupExpiredEntries();
        }

        return Task.CompletedTask;
    }

    public void InvalidateCache(string? pattern = null)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            _searchCache.Clear();
            return;
        }

        var keysToRemove = _searchCache.Keys
            .Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _searchCache.TryRemove(key, out _);
        }
    }

    public CacheStats GetStats()
    {
        var entries = _searchCache.Values.ToList();
        return new CacheStats
        {
            TotalEntries = entries.Count,
            TotalHits = entries.Sum(e => e.Hits),
            ExpiredEntries = entries.Count(e => e.IsExpired),
            ApiCallsThisMinute = _apiCallsThisMinute,
            IsThrottled = ShouldThrottle()
        };
    }

    public bool ShouldThrottle()
    {
        lock (_rateLimitLock)
        {
            ResetMinuteCounterIfNeeded();
            return _apiCallsThisMinute >= MaxCallsPerMinute;
        }
    }

    public void RecordApiCall()
    {
        lock (_rateLimitLock)
        {
            ResetMinuteCounterIfNeeded();
            _apiCallsThisMinute++;
        }
    }

    private void ResetMinuteCounterIfNeeded()
    {
        var now = DateTime.UtcNow;
        if ((now - _minuteStart).TotalMinutes >= 1)
        {
            _minuteStart = now;
            _apiCallsThisMinute = 0;
        }
    }

    private void CleanupExpiredEntries()
    {
        var expiredKeys = _searchCache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _searchCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Get or create a lock for deduplicating concurrent identical requests
    /// </summary>
    public SemaphoreSlim GetRequestLock(string key)
    {
        return _requestLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }
}

public class CacheEntry<T>
{
    public T Value { get; }
    public DateTime ExpiresAt { get; }
    public DateTime CreatedAt { get; }
    public int Hits { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public CacheEntry(T value, TimeSpan ttl)
    {
        Value = value;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt + ttl;
        Hits = 0;
    }
}

public class CacheStats
{
    public int TotalEntries { get; set; }
    public int TotalHits { get; set; }
    public int ExpiredEntries { get; set; }
    public int ApiCallsThisMinute { get; set; }
    public bool IsThrottled { get; set; }
}
