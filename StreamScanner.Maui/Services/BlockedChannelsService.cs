using System.Text.Json;

namespace StreamScanner.Maui.Services;

public interface IBlockedChannelsService
{
    List<string> GetBlockedChannels();
    bool IsBlocked(string channelTitle);
    void BlockChannel(string channelTitle);
    void UnblockChannel(string channelTitle);
    void ClearAll();
    event Action? BlockListChanged;
}

public class BlockedChannelsService : IBlockedChannelsService
{
    private const string StorageKey = "blocked_channels";
    private List<string> _blockedChannels;

    public event Action? BlockListChanged;

    public BlockedChannelsService()
    {
        _blockedChannels = LoadFromStorage();
    }

    public List<string> GetBlockedChannels() => new(_blockedChannels);

    public bool IsBlocked(string channelTitle)
    {
        return _blockedChannels.Any(b =>
            string.Equals(b, channelTitle, StringComparison.OrdinalIgnoreCase));
    }

    public void BlockChannel(string channelTitle)
    {
        var normalized = channelTitle.Trim();
        if (string.IsNullOrEmpty(normalized)) return;
        if (IsBlocked(normalized)) return;

        _blockedChannels.Add(normalized);
        SaveToStorage();
        BlockListChanged?.Invoke();
    }

    public void UnblockChannel(string channelTitle)
    {
        _blockedChannels.RemoveAll(b =>
            string.Equals(b, channelTitle, StringComparison.OrdinalIgnoreCase));
        SaveToStorage();
        BlockListChanged?.Invoke();
    }

    public void ClearAll()
    {
        _blockedChannels.Clear();
        SaveToStorage();
        BlockListChanged?.Invoke();
    }

    private List<string> LoadFromStorage()
    {
        try
        {
            var json = Preferences.Get(StorageKey, "[]");
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void SaveToStorage()
    {
        var json = JsonSerializer.Serialize(_blockedChannels);
        Preferences.Set(StorageKey, json);
    }
}
