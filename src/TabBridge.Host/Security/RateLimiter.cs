namespace TabBridge.Host.Security;

/// <summary>
/// Token-bucket rate limiter. One bucket per profile, keyed by profile name.
/// Thread-safe.
/// </summary>
public sealed class RateLimiter
{
    private readonly int _capacity;
    private readonly double _refillPerSecond;
    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <param name="capacity">Maximum burst size.</param>
    /// <param name="refillPerSecond">Tokens added per second.</param>
    public RateLimiter(int capacity = 20, double refillPerSecond = 5.0)
    {
        _capacity = capacity;
        _refillPerSecond = refillPerSecond;
    }

    /// <summary>
    /// Attempts to consume one token for <paramref name="profileName"/>.
    /// Returns <c>false</c> if the bucket is empty (rate limit exceeded).
    /// </summary>
    public bool TryConsume(string profileName)
    {
        lock (_lock)
        {
            if (!_buckets.TryGetValue(profileName, out Bucket? bucket))
            {
                bucket = new Bucket(_capacity, DateTimeOffset.UtcNow);
                _buckets[profileName] = bucket;
            }

            return bucket.TryConsume(_capacity, _refillPerSecond);
        }
    }

    private sealed class Bucket(double tokens, DateTimeOffset lastRefill)
    {
        private double _tokens = tokens;
        private DateTimeOffset _lastRefill = lastRefill;

        public bool TryConsume(int capacity, double refillPerSecond)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            double elapsed = (now - _lastRefill).TotalSeconds;
            _tokens = Math.Min(capacity, _tokens + elapsed * refillPerSecond);
            _lastRefill = now;

            if (_tokens < 1.0) return false;
            _tokens -= 1.0;
            return true;
        }
    }
}
