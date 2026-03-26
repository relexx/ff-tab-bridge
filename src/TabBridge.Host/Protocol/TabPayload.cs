using System.Text.Json.Serialization;

namespace TabBridge.Host.Protocol;

/// <summary>Payload carried inside a <see cref="BridgeMessage"/> of type TAB_SEND.</summary>
public record TabPayload(
    [property: JsonPropertyName("url")]      string Url,
    [property: JsonPropertyName("title")]    string Title,
    [property: JsonPropertyName("pinned")]   bool Pinned,
    [property: JsonPropertyName("group_id")] string? GroupId);
