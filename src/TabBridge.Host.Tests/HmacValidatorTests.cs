using TabBridge.Host.Protocol;
using TabBridge.Host.Security;

namespace TabBridge.Host.Tests;

public sealed class HmacValidatorTests
{
    private static readonly byte[] ValidKey = new byte[32];

    [Fact]
    public void Validate_ReturnsFalse_WhenHmacIsEmpty()
    {
        HmacValidator validator = new(ValidKey);
        BridgeMessage message = BuildMessage(hmac: string.Empty);

        bool result = validator.Validate(message);

        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenHmacIsTampered()
    {
        HmacValidator validator = new(ValidKey);
        BridgeMessage message = BuildMessage(hmac: "AAAA");

        bool result = validator.Validate(message);

        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_ReturnsTrue_WhenHmacIsCorrect()
    {
        HmacValidator validator = new(ValidKey);
        BridgeMessage template = BuildMessage(hmac: string.Empty);
        string correctHmac = validator.Compute(template);
        BridgeMessage signed = template with { Hmac = correctHmac };

        bool result = validator.Validate(signed);

        result.Should().BeTrue();
    }

    private static BridgeMessage BuildMessage(string hmac) => new(
        Version: 1,
        Type: MessageType.TAB_SEND,
        Id: Guid.NewGuid().ToString(),
        Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        SourceProfile: "Work",
        TargetProfile: "Personal",
        Payload: null,
        Hmac: hmac);
}
