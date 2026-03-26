using System.Text.Json;
using TabBridge.Host.Protocol;

namespace TabBridge.Host.Nmh;

/// <summary>
/// Writes Native Messaging Host messages to a stream using the browser wire format:
/// 4-byte little-endian length prefix followed by UTF-8 JSON.
/// </summary>
public sealed class NativeMessageWriter(Stream output)
{
    /// <summary>Serializes and writes <paramref name="message"/> to the output stream.</summary>
    public async Task WriteAsync(BridgeMessage message, CancellationToken cancellationToken)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(message, BridgeMessageContext.Default.BridgeMessage);

        if (json.Length > NativeMessageReader.MaxMessageBytes)
            throw new InvalidOperationException($"Outbound message ({json.Length} bytes) exceeds 64 KiB limit.");

        byte[] lengthPrefix = BitConverter.GetBytes((uint)json.Length);
        await output.WriteAsync(lengthPrefix, cancellationToken);
        await output.WriteAsync(json, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }
}
