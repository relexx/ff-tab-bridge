using System.Text.Json.Serialization;

namespace TabBridge.Host.Protocol;

/// <summary>
/// Payload for <see cref="MessageType.PROFILE_LIST_RESPONSE"/> messages sent by the broker.
/// Contains the list of currently registered profiles (excluding the requester).
/// </summary>
public record ProfileListPayload(
    [property: JsonPropertyName("profiles")] IReadOnlyList<ProfileEntry> Profiles);

/// <summary>A single profile entry in a <see cref="ProfileListPayload"/>.</summary>
public record ProfileEntry(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("avatar")]      string Avatar,
    [property: JsonPropertyName("theme_color")] string ThemeColor);
