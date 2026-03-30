using TabBridge.Host.Protocol;
using TabBridge.Host.Security;

namespace TabBridge.Host.Broker;

/// <summary>
/// Builds broker-originated messages (ACK, PROFILE_LIST_RESPONSE) with correct fields and HMAC.
/// </summary>
internal static class BrokerMessageFactory
{
    /// <summary>
    /// Creates a signed ACK in response to a REGISTER (or any) message.
    /// The ACK targets the sender's <see cref="BridgeMessage.SourceProfile"/>.
    /// </summary>
    internal static BridgeMessage CreateAck(BridgeMessage request, HmacValidator hmac)
    {
        BridgeMessage ack = new(
            Version: 1,
            Type: MessageType.ACK,
            Id: Guid.NewGuid().ToString("D"),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            SourceProfile: "broker",
            TargetProfile: request.SourceProfile,
            Payload: null,
            Hmac: "");

        return ack with { Hmac = hmac.Compute(ack) };
    }

    /// <summary>
    /// Creates a signed PROFILE_LIST_RESPONSE listing the profiles in <paramref name="otherProfileNames"/>
    /// (already filtered to exclude the requester).
    /// </summary>
    internal static BridgeMessage CreateProfileListResponse(
        BridgeMessage request,
        IEnumerable<string> otherProfileNames,
        HmacValidator hmac)
    {
        IReadOnlyList<ProfileEntry> entries = otherProfileNames
            .Select(name => new ProfileEntry(name, "", ""))
            .ToList();

        BridgeMessage response = new(
            Version: 1,
            Type: MessageType.PROFILE_LIST_RESPONSE,
            Id: Guid.NewGuid().ToString("D"),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            SourceProfile: "broker",
            TargetProfile: request.SourceProfile,
            Payload: null,
            Hmac: "",
            Profiles: new ProfileListPayload(entries));

        return response with { Hmac = hmac.Compute(response) };
    }
}
