using Microsoft.Extensions.Logging.Abstractions;
using TabBridge.Host.Broker;
using TabBridge.Host.Protocol;
using TabBridge.Host.Security;

namespace TabBridge.Host.Tests;

/// <summary>
/// Unit tests for broker components: <see cref="ShutdownTimer"/> and <see cref="BrokerMessageFactory"/>.
/// No Named Pipe infrastructure is required.
/// </summary>
public sealed class BrokerComponentTests
{
    private static readonly byte[] TestKey = new byte[32];  // 32 zero bytes – valid key for tests

    // ── ShutdownTimer ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ShutdownTimer_CancelsToken_AfterIdleTimeout()
    {
        using CancellationTokenSource cts = new();
        using ShutdownTimer timer = new(NullLoggerFactory.Instance, cts, TimeSpan.FromMilliseconds(30));

        timer.Reset();
        await Task.Delay(150); // wait well past the 30 ms timeout

        cts.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task ShutdownTimer_DoesNotCancelToken_WhenCancelledBeforeFiring()
    {
        using CancellationTokenSource cts = new();
        using ShutdownTimer timer = new(NullLoggerFactory.Instance, cts, TimeSpan.FromMilliseconds(200));

        timer.Reset();
        timer.Cancel();     // disarm before it fires
        await Task.Delay(50);

        cts.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task ShutdownTimer_RearmAfterCancel_FiresAgain()
    {
        using CancellationTokenSource cts = new();
        using ShutdownTimer timer = new(NullLoggerFactory.Instance, cts, TimeSpan.FromMilliseconds(30));

        timer.Reset();
        timer.Cancel();     // disarm
        timer.Reset();      // re-arm with same timeout
        await Task.Delay(150);

        cts.IsCancellationRequested.Should().BeTrue();
    }

    // ── BrokerMessageFactory – CreateAck ──────────────────────────────────────

    [Fact]
    public void CreateAck_ReturnsAckType()
    {
        HmacValidator hmac = new(TestKey);
        BridgeMessage register = BuildRegister("Work");

        BridgeMessage ack = BrokerMessageFactory.CreateAck(register, hmac);

        ack.Type.Should().Be(MessageType.ACK);
    }

    [Fact]
    public void CreateAck_TargetsRequestingProfile()
    {
        HmacValidator hmac = new(TestKey);
        BridgeMessage register = BuildRegister("Personal");

        BridgeMessage ack = BrokerMessageFactory.CreateAck(register, hmac);

        ack.TargetProfile.Should().Be("Personal");
        ack.SourceProfile.Should().Be("broker");
    }

    [Fact]
    public void CreateAck_HasValidHmac()
    {
        HmacValidator hmac = new(TestKey);
        BridgeMessage register = BuildRegister("Work");

        BridgeMessage ack = BrokerMessageFactory.CreateAck(register, hmac);

        hmac.Validate(ack).Should().BeTrue();
    }

    [Fact]
    public void CreateAck_HasNoTabPayload()
    {
        HmacValidator hmac = new(TestKey);
        BridgeMessage ack = BrokerMessageFactory.CreateAck(BuildRegister("Work"), hmac);

        ack.Payload.Should().BeNull();
    }

    // ── BrokerMessageFactory – CreateProfileListResponse ────────────────────

    [Fact]
    public void CreateProfileListResponse_ReturnsCorrectType()
    {
        HmacValidator hmac = new(TestKey);
        BridgeMessage request = BuildProfileListRequest("Work");

        BridgeMessage response = BrokerMessageFactory.CreateProfileListResponse(
            request, ["Personal", "Gaming"], hmac);

        response.Type.Should().Be(MessageType.PROFILE_LIST_RESPONSE);
    }

    [Fact]
    public void CreateProfileListResponse_ContainsAllSuppliedProfiles()
    {
        HmacValidator hmac = new(TestKey);
        BridgeMessage request = BuildProfileListRequest("Work");

        BridgeMessage response = BrokerMessageFactory.CreateProfileListResponse(
            request, ["Personal", "Gaming"], hmac);

        response.Profiles!.Profiles.Should().HaveCount(2);
        response.Profiles.Profiles.Select(p => p.Name).Should().BeEquivalentTo("Personal", "Gaming");
    }

    [Fact]
    public void CreateProfileListResponse_TargetsRequester()
    {
        HmacValidator hmac = new(TestKey);
        BridgeMessage request = BuildProfileListRequest("Work");

        BridgeMessage response = BrokerMessageFactory.CreateProfileListResponse(
            request, ["Personal"], hmac);

        response.TargetProfile.Should().Be("Work");
    }

    [Fact]
    public void CreateProfileListResponse_HasValidHmac()
    {
        HmacValidator hmac = new(TestKey);
        BridgeMessage request = BuildProfileListRequest("Work");

        BridgeMessage response = BrokerMessageFactory.CreateProfileListResponse(
            request, ["Personal"], hmac);

        hmac.Validate(response).Should().BeTrue();
    }

    [Fact]
    public void CreateProfileListResponse_EmptyList_WhenNoOtherProfiles()
    {
        HmacValidator hmac = new(TestKey);
        BridgeMessage request = BuildProfileListRequest("Work");

        BridgeMessage response = BrokerMessageFactory.CreateProfileListResponse(
            request, [], hmac);

        response.Profiles!.Profiles.Should().BeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BridgeMessage BuildRegister(string profile) =>
        new(1, MessageType.REGISTER,
            "550e8400-e29b-41d4-a716-446655440000",
            1_711_468_800L,
            profile, profile,
            null,
            "hmac-placeholder");

    private static BridgeMessage BuildProfileListRequest(string profile) =>
        new(1, MessageType.PROFILE_LIST_REQUEST,
            "550e8400-e29b-41d4-a716-446655440001",
            1_711_468_800L,
            profile, profile,
            null,
            "hmac-placeholder");
}
