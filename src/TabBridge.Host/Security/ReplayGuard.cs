namespace TabBridge.Host.Security;

/// <summary>
/// Prevents replay attacks by tracking seen message IDs within a 60-second window.
/// Thread-safe.
/// </summary>
public sealed class ReplayGuard
{
    private readonly TimeSpan _window;
    private readonly Dictionary<string, long> _seen = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <param name="window">How long a nonce is remembered. Defaults to 60 seconds.</param>
    public ReplayGuard(TimeSpan? window = null)
    {
        _window = window ?? TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Returns <c>true</c> and records the nonce if it has not been seen within the window.
    /// Returns <c>false</c> if the nonce is a replay.
    /// </summary>
    public bool TryAdd(string messageId, long timestampSeconds)
    {
        long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        lock (_lock)
        {
            Evict(nowSeconds);

            if (_seen.ContainsKey(messageId))
                return false;

            _seen[messageId] = timestampSeconds;
            return true;
        }
    }

    private void Evict(long nowSeconds)
    {
        long cutoff = nowSeconds - (long)_window.TotalSeconds;
        foreach (string key in _seen.Keys.Where(k => _seen[k] < cutoff).ToList())
            _seen.Remove(key);
    }
}
