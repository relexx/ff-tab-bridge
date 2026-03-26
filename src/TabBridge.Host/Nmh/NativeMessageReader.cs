using System.Text;
using System.Text.Json;
using TabBridge.Host.Protocol;

namespace TabBridge.Host.Nmh;

/// <summary>
/// Reads Native Messaging Host messages from a stream using the browser wire format:
/// 4-byte little-endian length prefix followed by UTF-8 JSON.
/// </summary>
public sealed class NativeMessageReader(Stream input)
{
    /// <summary>Maximum message size enforced before JSON parse (security rule #8).</summary>
    public const int MaxMessageBytes = 64 * 1024; // 64 KiB

    /// <summary>
    /// Reads one message from the stream.
    /// Returns <c>null</c> if the stream is closed cleanly.
    /// </summary>
    /// <exception cref="InvalidDataException">Message exceeds <see cref="MaxMessageBytes"/>.</exception>
    public async Task<BridgeMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        byte[] lengthBuf = new byte[4];
        int read = await ReadExactAsync(input, lengthBuf, cancellationToken);
        if (read == 0) return null; // stream closed

        uint length = BitConverter.ToUInt32(lengthBuf, 0);

        // Security rule #8: enforce size limit before JSON parse
        if (length > MaxMessageBytes)
            throw new InvalidDataException($"Message length {length} exceeds limit {MaxMessageBytes}.");

        byte[] jsonBuf = new byte[length];
        await ReadExactAsync(input, jsonBuf, cancellationToken);

        return JsonSerializer.Deserialize(jsonBuf, BridgeMessageContext.Default.BridgeMessage);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (bytesRead == 0) return totalRead;
            totalRead += bytesRead;
        }
        return totalRead;
    }
}
