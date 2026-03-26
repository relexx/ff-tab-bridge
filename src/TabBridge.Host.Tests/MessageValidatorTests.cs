using TabBridge.Host.Protocol;
using TabBridge.Host.Security;

namespace TabBridge.Host.Tests;

public sealed class MessageValidatorTests
{
    private readonly MessageValidator _sut = new();

    [Fact]
    public void Validate_ReturnsNull_WhenMessageIsValid()
    {
        BridgeMessage message = BuildMessage("https://example.com");

        string? error = _sut.Validate(message);

        error.Should().BeNull();
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void Validate_ReturnsError_WhenUrlSchemeNotAllowed(string url)
    {
        BridgeMessage message = BuildMessage(url);

        string? error = _sut.Validate(message);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_ReturnsError_WhenVersionIsUnsupported()
    {
        BridgeMessage message = BuildMessage("https://example.com") with { Version = 99 };

        string? error = _sut.Validate(message);

        error.Should().NotBeNull();
    }

    private static BridgeMessage BuildMessage(string url) => new(
        Version: 1,
        Type: MessageType.TAB_SEND,
        Id: Guid.NewGuid().ToString(),
        Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        SourceProfile: "Work",
        TargetProfile: "Personal",
        Payload: new TabPayload(url, "Title", false, null),
        Hmac: "placeholder");
}
