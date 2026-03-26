using TabBridge.Host.Protocol;

namespace TabBridge.Host.Security;

/// <summary>Validates incoming messages against schema rules and the URL whitelist.</summary>
public sealed class MessageValidator
{
    private static readonly HashSet<string> AllowedSchemes = ["http", "https"];

    /// <summary>
    /// Validates <paramref name="message"/> against structural rules.
    /// Returns a non-null error string on failure.
    /// </summary>
    public string? Validate(BridgeMessage message)
    {
        if (message.Version != 1)
            return $"Unsupported version: {message.Version}";

        if (string.IsNullOrWhiteSpace(message.Id))
            return "Message id is required";

        if (message.Timestamp <= 0)
            return "Message timestamp is required";

        if (message.Payload is not null)
            return ValidatePayload(message.Payload);

        return null;
    }

    private static string? ValidatePayload(TabPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Url))
            return "Payload url is required";

        // Security rule #7: URL whitelist – only http/https
        if (!Uri.TryCreate(payload.Url, UriKind.Absolute, out Uri? uri))
            return $"Payload url is not a valid URI: {payload.Url}";

        if (!AllowedSchemes.Contains(uri.Scheme))
            return $"Payload url scheme not allowed: {uri.Scheme}";

        return null;
    }
}
