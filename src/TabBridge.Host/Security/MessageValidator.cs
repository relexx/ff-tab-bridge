using System.Text.RegularExpressions;
using TabBridge.Host.Protocol;

namespace TabBridge.Host.Security;

/// <summary>
/// Validates incoming messages through the Stage 1 (structural) and Stage 2 (payload) pipeline
/// defined in SECURITY.md §2.5. Deserialization-level guards (MaxDepth = 4, size limit) are
/// enforced separately in <c>NativeMessageReader</c>.
/// </summary>
public sealed partial class MessageValidator
{
    // Stage 1 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// UUID v4 pattern: <c>xxxxxxxx-xxxx-4xxx-[89ab]xxx-xxxxxxxxxxxx</c> (case-insensitive).
    /// </summary>
    [GeneratedRegex(
        @"^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex UuidV4Pattern();

    /// <summary>Maximum allowed timestamp drift from local clock (seconds).</summary>
    private const int MaxTimestampDriftSeconds = 30;

    // Stage 2 ─────────────────────────────────────────────────────────────────

    private static readonly HashSet<string> AllowedUrlSchemes = ["http", "https"];

    /// <summary>
    /// Schemes explicitly blocked per SECURITY.md §2.5. The whitelist check is authoritative;
    /// this set provides specific error messages for high-risk schemes.
    /// </summary>
    private static readonly HashSet<string> BlockedUrlSchemes =
    [
        "javascript", "data", "file", "about", "chrome",
        "moz-extension", "blob", "resource", "vbscript"
    ];

    private const int MaxTitleLength = 512;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates <paramref name="message"/> against the Stage 1 and Stage 2 rules.
    /// Returns <c>null</c> on success, or a human-readable error string on failure.
    /// </summary>
    public string? Validate(BridgeMessage message)
    {
        if (message.Version != 1)
            return $"Unsupported protocol version {message.Version}. Expected 1.";

        if (string.IsNullOrWhiteSpace(message.Id))
            return "Field 'id' is required.";

        if (!UuidV4Pattern().IsMatch(message.Id))
            return $"Field 'id' must be a valid UUID v4 (got: '{message.Id}').";

        long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (message.Timestamp <= 0)
            return "Field 'timestamp' must be a positive Unix timestamp.";

        long drift = Math.Abs(nowSeconds - message.Timestamp);
        if (drift > MaxTimestampDriftSeconds)
            return $"Timestamp drift {drift}s exceeds maximum {MaxTimestampDriftSeconds}s. Possible replay or clock skew.";

        if (string.IsNullOrEmpty(message.Hmac))
            return "Field 'hmac' is required.";

        if (message.Payload is not null)
            return ValidatePayload(message.Payload);

        return null;
    }

    // ── Stage 2: payload ──────────────────────────────────────────────────────

    private static string? ValidatePayload(TabPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Url))
            return "Payload field 'url' is required.";

        if (!Uri.TryCreate(payload.Url, UriKind.Absolute, out Uri? uri))
            return $"Payload 'url' is not a valid absolute URI: '{payload.Url}'.";

        // High-risk scheme blocklist (defense-in-depth; whitelist below is authoritative)
        if (BlockedUrlSchemes.Contains(uri.Scheme))
            return $"Payload 'url' scheme '{uri.Scheme}' is explicitly prohibited.";

        // Security rule #7: only http and https are permitted
        if (!AllowedUrlSchemes.Contains(uri.Scheme))
            return $"Payload 'url' scheme '{uri.Scheme}' is not permitted. Allowed: http, https.";

        if (payload.Title is not null && payload.Title.Length > MaxTitleLength)
            return $"Payload 'title' exceeds maximum {MaxTitleLength} characters (got {payload.Title.Length}).";

        return null;
    }
}
