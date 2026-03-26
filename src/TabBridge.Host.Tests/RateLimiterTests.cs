using TabBridge.Host.Security;

namespace TabBridge.Host.Tests;

public sealed class RateLimiterTests
{
    [Fact]
    public void TryConsume_ReturnsTrue_WithinCapacity()
    {
        RateLimiter limiter = new(capacity: 5, refillPerSecond: 0);

        bool result = limiter.TryConsume("profile-A");

        result.Should().BeTrue();
    }

    [Fact]
    public void TryConsume_ReturnsFalse_WhenBucketExhausted()
    {
        RateLimiter limiter = new(capacity: 2, refillPerSecond: 0);
        limiter.TryConsume("profile-A");
        limiter.TryConsume("profile-A");

        bool result = limiter.TryConsume("profile-A");

        result.Should().BeFalse();
    }

    [Fact]
    public void TryConsume_IsolatesBucketsByProfile()
    {
        RateLimiter limiter = new(capacity: 1, refillPerSecond: 0);
        limiter.TryConsume("profile-A");

        bool result = limiter.TryConsume("profile-B");

        result.Should().BeTrue();
    }
}
