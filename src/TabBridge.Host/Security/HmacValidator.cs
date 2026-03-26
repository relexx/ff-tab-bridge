using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabBridge.Host.Protocol;

namespace TabBridge.Host.Security;

/// <summary>
/// Generates and validates HMAC-SHA256 signatures for bridge messages.
/// <para>
/// Canonical form: all non-<c>hmac</c> fields serialized as compact JSON with alphabetically
/// sorted keys. This is deterministic regardless of runtime property order.
/// </para>
/// </summary>
public sealed class HmacValidator
{
    private readonly byte[] _key;

    /// <param name="key">256-bit HMAC secret (32 bytes).</param>
    /// <exception cref="ArgumentException">Key is not exactly 32 bytes.</exception>
    public HmacValidator(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != 32)
            throw new ArgumentException("Key must be exactly 32 bytes (256 bits).", nameof(key));
        _key = key;
    }

    /// <summary>
    /// Computes the HMAC-SHA256 over the canonical JSON of <paramref name="message"/>
    /// (all fields except <c>hmac</c>, compact, keys in alphabetical order).
    /// Returns a Base64-encoded string.
    /// </summary>
    public string Compute(BridgeMessage message)
    {
        byte[] canonical = BuildCanonicalBytes(message);
        byte[] hash = HMACSHA256.HashData(_key, canonical);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Validates the HMAC on <paramref name="message"/> using a constant-time comparison.
    /// Returns <c>false</c> if the signature is absent, malformed, or incorrect.
    /// </summary>
    public bool Validate(BridgeMessage message)
    {
        if (string.IsNullOrEmpty(message.Hmac)) return false;

        byte[] expected;
        try { expected = Convert.FromBase64String(Compute(message)); }
        catch (FormatException) { return false; }

        byte[] actual;
        try { actual = Convert.FromBase64String(message.Hmac); }
        catch (FormatException) { return false; }

        if (expected.Length != actual.Length) return false;

        // Security rule #5: constant-time comparison
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    // ── Canonical form ────────────────────────────────────────────────────────

    private static byte[] BuildCanonicalBytes(BridgeMessage message)
    {
        // Canonical JSON: fields in alphabetical key order, compact (no whitespace).
        // Properties are declared in alphabetical JsonPropertyName order in SigningData
        // so System.Text.Json source gen serializes them in that order deterministically.
        SigningPayloadData? payload = message.Payload is null ? null : new SigningPayloadData(
            GroupId: message.Payload.GroupId,
            Pinned: message.Payload.Pinned,
            Title: message.Payload.Title,
            Url: message.Payload.Url);

        SigningData data = new(
            Id: message.Id,
            Payload: payload,
            SourceProfile: message.SourceProfile,
            TargetProfile: message.TargetProfile,
            Timestamp: message.Timestamp,
            Type: message.Type.ToString(),   // enum as string: "TAB_SEND", not integer
            Version: message.Version);

        return JsonSerializer.SerializeToUtf8Bytes(data, SigningContext.Default.SigningData);
    }
}

// ── Signing types (internal – not part of the wire protocol) ─────────────────

/// <summary>
/// Canonical signing payload – JSON property names in strict alphabetical order.
/// <c>group_id &lt; pinned &lt; title &lt; url</c>
/// </summary>
internal record SigningPayloadData(
    [property: JsonPropertyName("group_id")] string? GroupId,
    [property: JsonPropertyName("pinned")]   bool Pinned,
    [property: JsonPropertyName("title")]    string Title,
    [property: JsonPropertyName("url")]      string Url);

/// <summary>
/// Canonical signing envelope – JSON property names in strict alphabetical order.
/// <c>id &lt; payload &lt; source_profile &lt; target_profile &lt; timestamp &lt; type &lt; version</c>
/// </summary>
internal record SigningData(
    [property: JsonPropertyName("id")]             string Id,
    [property: JsonPropertyName("payload")]        SigningPayloadData? Payload,
    [property: JsonPropertyName("source_profile")] string SourceProfile,
    [property: JsonPropertyName("target_profile")] string TargetProfile,
    [property: JsonPropertyName("timestamp")]      long Timestamp,
    [property: JsonPropertyName("type")]           string Type,
    [property: JsonPropertyName("version")]        int Version);

[JsonSerializable(typeof(SigningData))]
[JsonSerializable(typeof(SigningPayloadData))]
internal partial class SigningContext : JsonSerializerContext { }
