using System.Text.Json.Serialization;

namespace TabBridge.Host.Protocol;

/// <summary>Root message envelope exchanged between extension, NMH, and broker.</summary>
public record BridgeMessage(
    [property: JsonPropertyName("version")]        int Version,
    [property: JsonPropertyName("type")]           MessageType Type,
    [property: JsonPropertyName("id")]             string Id,
    [property: JsonPropertyName("timestamp")]      long Timestamp,
    [property: JsonPropertyName("source_profile")] string SourceProfile,
    [property: JsonPropertyName("target_profile")] string TargetProfile,
    [property: JsonPropertyName("payload")]        TabPayload? Payload,
    [property: JsonPropertyName("hmac")]           string Hmac);

/// <summary>Source-generation context for trim-compatible serialization.</summary>
[JsonSerializable(typeof(BridgeMessage))]
[JsonSerializable(typeof(TabPayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
public partial class BridgeMessageContext : JsonSerializerContext { }
