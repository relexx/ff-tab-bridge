using System.Text.Json;
using TabBridge.Host.Protocol;

namespace TabBridge.Host.Nmh;

/// <summary>
/// Reads Native Messaging Host messages from a stream using the browser wire format:
/// 4-byte little-endian length prefix followed by UTF-8 JSON body.
/// </summary>
public sealed class NativeMessageReader(Stream input)
{
    /// <summary>Maximum message body size enforced before JSON parse (security rule #8).</summary>
    public const int MaxMessageBytes = 64 * 1024; // 64 KiB

    /// <summary>
    /// Deserialization context with <see cref="JsonSerializerOptions.MaxDepth"/> = 4
    /// (SECURITY.md §2.5 Stage 0). Created once and reused.
    /// </summary>
    private static readonly BridgeMessageContext ReadContext =
        new(new JsonSerializerOptions { MaxDepth = 4 });

    /// <summary>
    /// Reads one message from the stream.
    /// Returns <c>null</c> on a clean stream close (zero bytes on the length prefix).
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// Message body length exceeds <see cref="MaxMessageBytes"/>, or the stream is
    /// truncated mid-frame (partial length prefix or body).
    /// </exception>
    /// <exception cref="JsonException">
    /// Body JSON is malformed or violates the MaxDepth = 4 limit.
    /// </exception>
    public async Task<BridgeMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        // ── Length prefix ─────────────────────────────────────────────────────
        byte[] lengthBuf = new byte[4];
        int prefixRead = await ReadExactAsync(input, lengthBuf, cancellationToken);

        if (prefixRead == 0) return null;          // clean EOF
        if (prefixRead < 4)                        // stream closed mid-prefix
            throw new InvalidDataException(
                $"Stream closed after {prefixRead} of 4 length-prefix bytes.");

        uint bodyLength = BitConverter.ToUInt32(lengthBuf, 0); // little-endian

        // Security rule #8: enforce 64 KiB limit BEFORE allocating or parsing
        if (bodyLength > MaxMessageBytes)
            throw new InvalidDataException(
                $"Declared message length {bodyLength} B exceeds limit {MaxMessageBytes} B.");

        // ── Body ──────────────────────────────────────────────────────────────
        byte[] bodyBuf = new byte[bodyLength];
        int bodyRead = await ReadExactAsync(input, bodyBuf, cancellationToken);

        if (bodyRead < (int)bodyLength)
            throw new InvalidDataException(
                $"Stream closed after {bodyRead} of {bodyLength} body bytes.");

        // MaxDepth = 4 is enforced by ReadContext (SECURITY.md §2.5 Stage 1)
        return JsonSerializer.Deserialize(bodyBuf, ReadContext.BridgeMessage);
    }

    /// <summary>
    /// Reads exactly <c>buffer.Length</c> bytes. Returns the number of bytes actually read;
    /// a return value less than <c>buffer.Length</c> means the stream reached EOF.
    /// </summary>
    private static async Task<int> ReadExactAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);

            if (bytesRead == 0) return totalRead; // EOF
            totalRead += bytesRead;
        }
        return totalRead;
    }
}
