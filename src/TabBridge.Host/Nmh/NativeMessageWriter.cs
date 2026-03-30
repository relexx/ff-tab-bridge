using System.Text.Json;
using TabBridge.Host.Protocol;

namespace TabBridge.Host.Nmh;

/// <summary>
/// Writes Native Messaging Host messages to a stream using the browser wire format:
/// 4-byte little-endian length prefix followed by UTF-8 JSON body.
/// </summary>
public sealed class NativeMessageWriter(Stream output)
{
    /// <summary>
    /// Serializes <paramref name="message"/> and writes it as a single atomic frame
    /// (prefix + body in one buffer) to prevent partial writes on concurrent pipe use.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Serialized message body exceeds <see cref="NativeMessageReader.MaxMessageBytes"/>.
    /// </exception>
    public async Task WriteAsync(BridgeMessage message, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(
            message, BridgeMessageContext.Default.BridgeMessage);

        if (body.Length > NativeMessageReader.MaxMessageBytes)
            throw new InvalidOperationException(
                $"Outbound message body ({body.Length} B) exceeds 64 KiB limit.");

        // Build a single frame: [uint32-LE length][body] – one Write call is atomic on pipes
        byte[] frame = new byte[4 + body.Length];
        BitConverter.TryWriteBytes(frame, (uint)body.Length); // little-endian on all .NET platforms
        body.CopyTo(frame, 4);

        await output.WriteAsync(frame, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }
}
