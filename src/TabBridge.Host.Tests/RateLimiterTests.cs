using TabBridge.Host.Security;

namespace TabBridge.Host.Tests;

public sealed class RateLimiterTests
{
    // ── Basic capacity ────────────────────────────────────────────────────────

    [Fact]
    public void TryConsume_ReturnsTrue_OnFirstRequest()
    {
        RateLimiter limiter = new(capacity: 5, refillPerSecond: 0);

        limiter.TryConsume("profile-A").Should().BeTrue();
    }

    [Fact]
    public void TryConsume_ReturnsFalse_WhenBucketExhausted()
    {
        RateLimiter limiter = new(capacity: 2, refillPerSecond: 0);
        limiter.TryConsume("profile-A");
        limiter.TryConsume("profile-A");

        limiter.TryConsume("profile-A").Should().BeFalse();
    }

    [Fact]
    public void TryConsume_AllowsExactlyCapacityRequests()
    {
        const int capacity = 5;
        RateLimiter limiter = new(capacity: capacity, refillPerSecond: 0);

        List<bool> results = Enumerable.Range(0, capacity + 1)
            .Select(_ => limiter.TryConsume("p"))
            .ToList();

        results.Take(capacity).Should().AllSatisfy(r => r.Should().BeTrue());
        results.Last().Should().BeFalse();
    }

    // ── Profile isolation ─────────────────────────────────────────────────────

    [Fact]
    public void TryConsume_BucketsAreIsolatedByProfile()
    {
        RateLimiter limiter = new(capacity: 1, refillPerSecond: 0);
        limiter.TryConsume("profile-A"); // exhaust A's bucket

        limiter.TryConsume("profile-B").Should().BeTrue(); // B is unaffected
    }

    [Fact]
    public void TryConsume_ProfileNamesAreCaseInsensitive()
    {
        // "Work" and "work" should share the same bucket
        RateLimiter limiter = new(capacity: 1, refillPerSecond: 0);
        limiter.TryConsume("Work");

        limiter.TryConsume("work").Should().BeFalse();
    }

    [Fact]
    public void TryConsume_CreatesIndependentBucketPerNewProfile()
    {
        RateLimiter limiter = new(capacity: 3, refillPerSecond: 0);

        bool r1 = limiter.TryConsume("Alpha");
        bool r2 = limiter.TryConsume("Beta");
        bool r3 = limiter.TryConsume("Gamma");

        r1.Should().BeTrue();
        r2.Should().BeTrue();
        r3.Should().BeTrue();
    }

    // ── Token refill ──────────────────────────────────────────────────────────

    [Fact]
    public void TryConsume_ReturnsTrue_AfterRefillPeriod()
    {
        // Refill fast enough that one token is available after a brief wait
        RateLimiter limiter = new(capacity: 1, refillPerSecond: 100.0); // 100 tokens/s
        limiter.TryConsume("p"); // exhaust

        Thread.Sleep(20); // ~2 tokens refilled at 100/s

        limiter.TryConsume("p").Should().BeTrue();
    }

    [Fact]
    public void TryConsume_CapIsEnforced_OnFreshBucket()
    {
        // A fresh bucket starts at full capacity. With zero refill, it drains exactly to 0
        // and no more requests are permitted. This verifies the cap invariant without
        // any sleep or race against a high refill rate.
        const int capacity = 5;
        RateLimiter limiter = new(capacity: capacity, refillPerSecond: 0);

        int accepted = 0;
        for (int i = 0; i < capacity + 5; i++)
            if (limiter.TryConsume("p")) accepted++;

        accepted.Should().Be(capacity);
    }

    // ── Concurrency ───────────────────────────────────────────────────────────

    [Fact]
    public void TryConsume_IsThreadSafe_UnderConcurrentAccess()
    {
        const int capacity = 10;
        RateLimiter limiter = new(capacity: capacity, refillPerSecond: 0);
        int acceptCount = 0;

        Parallel.For(0, 50, _ =>
        {
            if (limiter.TryConsume("shared"))
                Interlocked.Increment(ref acceptCount);
        });

        acceptCount.Should().Be(capacity);
    }

    // ── Default parameters ────────────────────────────────────────────────────

    [Fact]
    public void DefaultConstructor_AllowsTwentyRequestsBeforeThrottling()
    {
        // Default capacity is 20 per SECURITY.md §2.5
        RateLimiter limiter = new();

        int count = 0;
        while (limiter.TryConsume("p")) count++;

        count.Should().Be(20);
    }
}
