using TabBridge.Host.Security;

namespace TabBridge.Host.Tests;

public sealed class ReplayGuardTests
{
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // ── Basic accept / reject ─────────────────────────────────────────────────

    [Fact]
    public void TryAdd_ReturnsTrue_ForFirstOccurrence()
    {
        ReplayGuard guard = new();

        guard.TryAdd("msg-1", Now).Should().BeTrue();
    }

    [Fact]
    public void TryAdd_ReturnsFalse_ForImmediateDuplicate()
    {
        ReplayGuard guard = new();
        long ts = Now;
        guard.TryAdd("msg-1", ts);

        guard.TryAdd("msg-1", ts).Should().BeFalse();
    }

    [Fact]
    public void TryAdd_ReturnsTrue_ForDistinctIds()
    {
        ReplayGuard guard = new();
        long ts = Now;

        bool first  = guard.TryAdd("msg-a", ts);
        bool second = guard.TryAdd("msg-b", ts);

        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public void TryAdd_ReturnsFalse_ForDuplicateWithDifferentTimestamp()
    {
        // Same id is a replay regardless of the timestamp
        ReplayGuard guard = new();
        guard.TryAdd("msg-x", Now);

        guard.TryAdd("msg-x", Now + 1).Should().BeFalse();
    }

    // ── Window expiry ─────────────────────────────────────────────────────────

    [Fact]
    public void TryAdd_ReturnsTrue_WhenEntryTimestampIsOlderThanWindow()
    {
        // Eviction is based on the stored timestamp vs. (nowSeconds - windowSeconds).
        // Storing an entry with a timestamp already older than the window means it is
        // evicted on the very next TryAdd call – no sleep required.
        ReplayGuard guard = new(window: TimeSpan.FromSeconds(60));
        long pastTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 61;
        guard.TryAdd("msg-old", pastTimestamp);

        // The same id should be accepted again because the stored entry was evicted
        guard.TryAdd("msg-old", Now).Should().BeTrue();
    }

    // ── Concurrency ───────────────────────────────────────────────────────────

    [Fact]
    public void TryAdd_IsThreadSafe_UnderConcurrentAccess()
    {
        ReplayGuard guard = new();
        long ts = Now;
        int acceptCount = 0;

        Parallel.For(0, 20, _ =>
        {
            if (guard.TryAdd("shared-id", ts))
                Interlocked.Increment(ref acceptCount);
        });

        // Exactly one thread should have accepted the id
        acceptCount.Should().Be(1);
    }

    [Fact]
    public void TryAdd_HandlesHighVolumeOfUniqueIds()
    {
        ReplayGuard guard = new();
        long ts = Now;
        List<string> ids = Enumerable.Range(0, 1000).Select(i => $"msg-{i}").ToList();

        List<bool> results = ids.Select(id => guard.TryAdd(id, ts)).ToList();

        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    // ── Default window ────────────────────────────────────────────────────────

    [Fact]
    public void TryAdd_HoldsIdForDefaultSixtySeconds()
    {
        // Verify entry is still rejected within the 60-second window
        ReplayGuard guard = new();
        long ts = Now;
        guard.TryAdd("msg-hold", ts);

        // Try again immediately (well within 60s)
        guard.TryAdd("msg-hold", ts).Should().BeFalse();
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void TryAdd_HandlesEmptyStringId()
    {
        ReplayGuard guard = new();

        bool first  = guard.TryAdd(string.Empty, Now);
        bool second = guard.TryAdd(string.Empty, Now);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public void TryAdd_IsCaseSensitive()
    {
        ReplayGuard guard = new();
        long ts = Now;

        bool lower = guard.TryAdd("msg-abc", ts);
        bool upper = guard.TryAdd("MSG-ABC", ts);

        lower.Should().BeTrue();
        upper.Should().BeTrue(); // different case = different id
    }
}
