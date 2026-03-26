using System.Text.Json.Serialization;

namespace TabBridge.Host.Protocol;

/// <summary>Whitelist of allowed message types. Any other value is rejected at deserialization.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MessageType>))]
public enum MessageType
{
    REGISTER,
    TAB_SEND,
    TAB_SEND_BATCH,
    ACK,
    NACK,
    HEARTBEAT,
    PROFILE_LIST_REQUEST,
    PROFILE_LIST_RESPONSE,
}
