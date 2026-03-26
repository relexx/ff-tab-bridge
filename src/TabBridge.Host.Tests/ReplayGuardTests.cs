using TabBridge.Host.Security;

namespace TabBridge.Host.Tests;

public sealed class ReplayGuardTests
{
    [Fact]
    public void TryAdd_ReturnsTrue_ForFirstOccurrence()
    {
        ReplayGuard guard = new();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        bool result = guard.TryAdd("msg-1", now);

        result.Should().BeTrue();
    }

    [Fact]
    public void TryAdd_ReturnsFalse_ForDuplicate()
    {
        ReplayGuard guard = new();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        guard.TryAdd("msg-1", now);

        bool result = guard.TryAdd("msg-1", now);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryAdd_ReturnsTrue_ForDifferentIds()
    {
        ReplayGuard guard = new();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        bool first = guard.TryAdd("msg-a", now);
        bool second = guard.TryAdd("msg-b", now);

        first.Should().BeTrue();
        second.Should().BeTrue();
    }
}
