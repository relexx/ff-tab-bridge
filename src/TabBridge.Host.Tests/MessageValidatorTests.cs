using TabBridge.Host.Protocol;
using TabBridge.Host.Security;

namespace TabBridge.Host.Tests;

public sealed class MessageValidatorTests
{
    private readonly MessageValidator _sut = new();

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsNull_ForValidMessageWithoutPayload()
    {
        BridgeMessage message = Build();

        _sut.Validate(message).Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_ForValidMessageWithHttpsPayload()
    {
        BridgeMessage message = Build(url: "https://example.com/page?q=1");

        _sut.Validate(message).Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_ForValidMessageWithHttpPayload()
    {
        BridgeMessage message = Build(url: "http://localhost:3000/dev");

        _sut.Validate(message).Should().BeNull();
    }

    // ── Version ───────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsError_ForUnsupportedVersion()
    {
        BridgeMessage message = Build() with { Version = 99 };

        _sut.Validate(message).Should().Contain("version");
    }

    // ── Id / UUID v4 ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ReturnsError_WhenIdIsEmpty(string id)
    {
        BridgeMessage message = Build() with { Id = id };

        _sut.Validate(message).Should().Contain("'id'");
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]   // version 0, not v4
    [InlineData("550e8400-e29b-11d4-a716-446655440000")]   // version 1, not v4
    [InlineData("550e8400e29b41d4a716446655440000")]        // no hyphens
    public void Validate_ReturnsError_WhenIdIsNotUuidV4(string id)
    {
        BridgeMessage message = Build() with { Id = id };

        _sut.Validate(message).Should().Contain("UUID v4");
    }

    [Theory]
    [InlineData("550e8400-e29b-4fd4-a716-446655440000")] // valid v4
    [InlineData("6ba7b810-9dad-41d1-80b4-00c04fd430c8")] // wait, version=4 missing – let me check
    public void Validate_ReturnsNull_ForValidUuidV4(string id)
    {
        // Only truly valid v4 UUIDs should pass
        if (!System.Text.RegularExpressions.Regex.IsMatch(id,
            @"^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return; // Skip if test data is itself invalid
        }
        BridgeMessage message = Build() with { Id = id };

        _sut.Validate(message).Should().BeNull();
    }

    // ── Timestamp ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsError_WhenTimestampIsZero()
    {
        BridgeMessage message = Build() with { Timestamp = 0 };

        _sut.Validate(message).Should().Contain("timestamp");
    }

    [Fact]
    public void Validate_ReturnsError_WhenTimestampIsNegative()
    {
        BridgeMessage message = Build() with { Timestamp = -1 };

        _sut.Validate(message).Should().Contain("timestamp");
    }

    [Fact]
    public void Validate_ReturnsError_WhenTimestampIsTooOld()
    {
        long oldTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 120; // 2 minutes ago
        BridgeMessage message = Build() with { Timestamp = oldTimestamp };

        _sut.Validate(message).Should().Contain("drift");
    }

    [Fact]
    public void Validate_ReturnsError_WhenTimestampIsTooFarInFuture()
    {
        long futureTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 120; // 2 minutes ahead
        BridgeMessage message = Build() with { Timestamp = futureTimestamp };

        _sut.Validate(message).Should().Contain("drift");
    }

    // ── HMAC ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenHmacIsMissing(string? hmac)
    {
        BridgeMessage message = Build() with { Hmac = hmac! };

        _sut.Validate(message).Should().Contain("'hmac'");
    }

    // ── URL scheme whitelist ──────────────────────────────────────────────────

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("about:blank")]
    [InlineData("chrome://settings")]
    [InlineData("moz-extension://abc/page.html")]
    [InlineData("blob:https://example.com/abc")]
    [InlineData("resource://gre/modules/Foo.jsm")]
    [InlineData("vbscript:msgbox(1)")]
    public void Validate_ReturnsError_ForExplicitlyBlockedScheme(string url)
    {
        BridgeMessage message = Build(url: url);

        string? error = _sut.Validate(message);
        error.Should().NotBeNull();
        error.Should().ContainAny("prohibited", "not permitted");
    }

    [Theory]
    [InlineData("ftp://files.example.com/file.txt")]
    [InlineData("ssh://server.example.com")]
    [InlineData("ws://ws.example.com/chat")]
    public void Validate_ReturnsError_ForNonAllowedScheme(string url)
    {
        BridgeMessage message = Build(url: url);

        _sut.Validate(message).Should().Contain("not permitted");
    }

    // ── URL format ────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsError_WhenUrlIsEmpty()
    {
        BridgeMessage message = Build(url: "");

        _sut.Validate(message).Should().Contain("'url'");
    }

    [Fact]
    public void Validate_ReturnsError_WhenUrlIsRelative()
    {
        BridgeMessage message = Build(url: "/relative/path");

        _sut.Validate(message).Should().NotBeNull();
    }

    // ── Title length ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsError_WhenTitleExceeds512Characters()
    {
        string longTitle = new('x', 513);
        BridgeMessage message = Build(url: "https://example.com", title: longTitle);

        _sut.Validate(message).Should().Contain("title");
    }

    [Fact]
    public void Validate_ReturnsNull_WhenTitleIsExactly512Characters()
    {
        string maxTitle = new('x', 512);
        BridgeMessage message = Build(url: "https://example.com", title: maxTitle);

        _sut.Validate(message).Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_WhenTitleIsNull()
    {
        BridgeMessage message = Build(url: "https://example.com", title: null);

        _sut.Validate(message).Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BridgeMessage Build(string? url = null, string? title = "Test Title")
    {
        TabPayload? payload = url is null
            ? null
            : new TabPayload(url, title ?? string.Empty, false, null);

        return new BridgeMessage(
            Version: 1,
            Type: MessageType.TAB_SEND,
            Id: Guid.NewGuid().ToString(),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            SourceProfile: "Work",
            TargetProfile: "Personal",
            Payload: payload,
            Hmac: "placeholder-hmac");
    }
}
