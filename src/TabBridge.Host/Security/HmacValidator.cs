using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TabBridge.Host.Protocol;

namespace TabBridge.Host.Security;

/// <summary>Generates and validates HMAC-SHA256 signatures for bridge messages.</summary>
public sealed class HmacValidator
{
    private readonly byte[] _key;

    /// <param name="key">256-bit HMAC secret loaded from <c>secret.key</c>.</param>
    public HmacValidator(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes (256 bits).", nameof(key));
        _key = key;
    }

    /// <summary>Computes the HMAC-SHA256 for the canonical representation of <paramref name="message"/>.</summary>
    public string Compute(BridgeMessage message)
    {
        byte[] payload = GetSigningBytes(message);
        byte[] hash = HMACSHA256.HashData(_key, payload);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Validates the HMAC on <paramref name="message"/> in constant time.
    /// Returns <c>false</c> if the signature is missing or incorrect.
    /// </summary>
    public bool Validate(BridgeMessage message)
    {
        if (string.IsNullOrEmpty(message.Hmac)) return false;

        byte[] expected = Convert.FromBase64String(Compute(message));
        byte[] actual;
        try { actual = Convert.FromBase64String(message.Hmac); }
        catch (FormatException) { return false; }

        // Security rule #5: must use FixedTimeEquals
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] GetSigningBytes(BridgeMessage message)
    {
        // Sign canonical fields only (exclude hmac itself)
        string canonical = $"{message.Version}|{message.Type}|{message.Id}|{message.Timestamp}|{message.SourceProfile}|{message.TargetProfile}";
        return Encoding.UTF8.GetBytes(canonical);
    }
}
