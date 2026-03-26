using TabBridge.Host.Protocol;
using TabBridge.Host.Security;

namespace TabBridge.Host.Tests;

public sealed class HmacValidatorTests
{
    private static readonly byte[] Key = new byte[32]; // all-zero test key

    // ── Compute ───────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_IsDeterministic_ForSameMessage()
    {
        HmacValidator v = new(Key);
        BridgeMessage msg = Build();

        string first = v.Compute(msg);
        string second = v.Compute(msg);

        first.Should().Be(second);
    }

    [Fact]
    public void Compute_ProducesBase64String()
    {
        HmacValidator v = new(Key);
        string hmac = v.Compute(Build());

        // Should not throw – valid Base64
        Action act = () => Convert.FromBase64String(hmac);
        act.Should().NotThrow();
    }

    [Fact]
    public void Compute_ProducesExactly32ByteHash()
    {
        HmacValidator v = new(Key);
        byte[] hash = Convert.FromBase64String(v.Compute(Build()));

        hash.Should().HaveCount(32); // HMAC-SHA256 = 32 bytes
    }

    [Fact]
    public void Compute_DiffersForDifferentTimestamps()
    {
        HmacValidator v = new(Key);
        BridgeMessage msg = Build();

        string h1 = v.Compute(msg with { Timestamp = 1000 });
        string h2 = v.Compute(msg with { Timestamp = 1001 });

        h1.Should().NotBe(h2);
    }

    [Fact]
    public void Compute_DiffersForDifferentIds()
    {
        HmacValidator v = new(Key);
        BridgeMessage msg = Build();

        string h1 = v.Compute(msg with { Id = Guid.NewGuid().ToString() });
        string h2 = v.Compute(msg with { Id = Guid.NewGuid().ToString() });

        h1.Should().NotBe(h2);
    }

    [Fact]
    public void Compute_DiffersForDifferentPayloadUrls()
    {
        HmacValidator v = new(Key);
        BridgeMessage withUrl1 = Build(url: "https://example.com");
        BridgeMessage withUrl2 = Build(url: "https://other.com");

        v.Compute(withUrl1).Should().NotBe(v.Compute(withUrl2));
    }

    [Fact]
    public void Compute_DiffersForNullVsNonNullPayload()
    {
        HmacValidator v = new(Key);
        BridgeMessage withPayload    = Build(url: "https://example.com");
        BridgeMessage withoutPayload = Build();

        v.Compute(withPayload).Should().NotBe(v.Compute(withoutPayload));
    }

    [Fact]
    public void Compute_DiffersForDifferentMessageTypes()
    {
        HmacValidator v = new(Key);
        BridgeMessage tabSend   = Build() with { Type = MessageType.TAB_SEND };
        BridgeMessage heartbeat = Build() with { Type = MessageType.HEARTBEAT };

        v.Compute(tabSend).Should().NotBe(v.Compute(heartbeat));
    }

    [Fact]
    public void Compute_DiffersForDifferentKeys()
    {
        byte[] key1 = new byte[32];
        byte[] key2 = new byte[32];
        key2[0] = 0xFF;

        HmacValidator v1 = new(key1);
        HmacValidator v2 = new(key2);
        BridgeMessage msg = Build();

        v1.Compute(msg).Should().NotBe(v2.Compute(msg));
    }

    // ── Validate: false cases ─────────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsFalse_WhenHmacIsEmpty()
    {
        HmacValidator v = new(Key);
        BridgeMessage msg = Build() with { Hmac = string.Empty };

        v.Validate(msg).Should().BeFalse();
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenHmacIsNotBase64()
    {
        HmacValidator v = new(Key);
        BridgeMessage msg = Build() with { Hmac = "not!!valid!!base64" };

        v.Validate(msg).Should().BeFalse();
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenHmacIsForDifferentMessage()
    {
        HmacValidator v = new(Key);
        BridgeMessage original = Build();
        string correctHmac = v.Compute(original);

        // Tamper with the timestamp after signing
        BridgeMessage tampered = original with { Timestamp = original.Timestamp + 1, Hmac = correctHmac };

        v.Validate(tampered).Should().BeFalse();
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenKeyIsWrong()
    {
        byte[] correctKey = new byte[32];
        byte[] wrongKey = new byte[32];
        wrongKey[31] = 0xAB;

        HmacValidator signer = new(correctKey);
        HmacValidator verifier = new(wrongKey);
        BridgeMessage msg = Build();
        string hmac = signer.Compute(msg);

        verifier.Validate(msg with { Hmac = hmac }).Should().BeFalse();
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenPayloadUrlChanged()
    {
        HmacValidator v = new(Key);
        BridgeMessage original = Build(url: "https://example.com");
        string hmac = v.Compute(original);

        BridgeMessage tampered = Build(url: "https://evil.com") with { Hmac = hmac, Id = original.Id, Timestamp = original.Timestamp };

        v.Validate(tampered).Should().BeFalse();
    }

    // ── Validate: true cases ──────────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsTrue_ForCorrectlySignedMessage()
    {
        HmacValidator v = new(Key);
        BridgeMessage msg = Build();
        BridgeMessage signed = msg with { Hmac = v.Compute(msg) };

        v.Validate(signed).Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsTrue_ForMessageWithPayload()
    {
        HmacValidator v = new(Key);
        BridgeMessage msg = Build(url: "https://example.com/page");
        BridgeMessage signed = msg with { Hmac = v.Compute(msg) };

        v.Validate(signed).Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsTrue_ForHeartbeatMessage()
    {
        HmacValidator v = new(Key);
        BridgeMessage msg = Build() with { Type = MessageType.HEARTBEAT };
        BridgeMessage signed = msg with { Hmac = v.Compute(msg) };

        v.Validate(signed).Should().BeTrue();
    }

    // ── Constructor validation ────────────────────────────────────────────────

    [Fact]
    public void Constructor_Throws_WhenKeyIsNot32Bytes()
    {
        Action act = () => _ = new HmacValidator(new byte[16]);

        act.Should().Throw<ArgumentException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void Constructor_Throws_WhenKeyIsNull()
    {
        Action act = () => _ = new HmacValidator(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BridgeMessage Build(string? url = null) =>
        new(
            Version: 1,
            Type: MessageType.TAB_SEND,
            Id: "550e8400-e29b-41d4-a716-446655440000",
            Timestamp: 1_711_468_800L,
            SourceProfile: "Work",
            TargetProfile: "Personal",
            Payload: url is null ? null : new TabPayload(url, "Title", false, null),
            Hmac: string.Empty);
}
