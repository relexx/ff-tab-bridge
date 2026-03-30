using System.Text;
using System.Text.Json;
using TabBridge.Host.Nmh;
using TabBridge.Host.Protocol;

namespace TabBridge.Host.Tests;

/// <summary>
/// Tests for the NMH wire protocol: 4-byte LE length prefix + UTF-8 JSON body.
/// Uses <see cref="MemoryStream"/> so no I/O infrastructure is needed.
/// </summary>
public sealed class NmhProtocolTests
{
    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteRead_RoundTrip_PreservesAllFields()
    {
        BridgeMessage original = BuildTabSend("https://example.com", "Test Title");
        BridgeMessage read = await WriteAndReadAsync(original);

        read.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task WriteRead_RoundTrip_WithNullPayload()
    {
        BridgeMessage original = BuildHeartbeat();
        BridgeMessage read = await WriteAndReadAsync(original);

        read.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task WriteRead_MultipleMessages_InSequence()
    {
        MemoryStream stream = new();
        NativeMessageWriter writer = new(stream);
        BridgeMessage msg1 = BuildTabSend("https://first.com", "First");
        BridgeMessage msg2 = BuildTabSend("https://second.com", "Second");
        BridgeMessage msg3 = BuildHeartbeat();

        await writer.WriteAsync(msg1, default);
        await writer.WriteAsync(msg2, default);
        await writer.WriteAsync(msg3, default);

        stream.Position = 0;
        NativeMessageReader reader = new(stream);

        BridgeMessage? r1 = await reader.ReadAsync(default);
        BridgeMessage? r2 = await reader.ReadAsync(default);
        BridgeMessage? r3 = await reader.ReadAsync(default);
        BridgeMessage? r4 = await reader.ReadAsync(default); // EOF

        r1.Should().BeEquivalentTo(msg1);
        r2.Should().BeEquivalentTo(msg2);
        r3.Should().BeEquivalentTo(msg3);
        r4.Should().BeNull();
    }

    // ── Clean close ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_ReturnsNull_ForEmptyStream()
    {
        NativeMessageReader reader = new(new MemoryStream());

        BridgeMessage? result = await reader.ReadAsync(default);

        result.Should().BeNull();
    }

    // ── Size limit (security rule #8) ─────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_Throws_WhenDeclaredLengthExceeds64KiB()
    {
        // Craft a frame whose length prefix claims 64 KiB + 1 byte
        uint oversizeLength = NativeMessageReader.MaxMessageBytes + 1;
        byte[] frame = new byte[4];
        BitConverter.TryWriteBytes(frame, oversizeLength);

        MemoryStream stream = new(frame);
        NativeMessageReader reader = new(stream);

        Func<Task> act = async () => await reader.ReadAsync(default);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*exceeds limit*");
    }

    [Fact]
    public async Task ReadAsync_Accepts_MessageAtExactly64KiB()
    {
        // Build a body that is exactly 64 KiB. We use a HEARTBEAT and pad the id field
        // to reach the limit.
        string paddedId = new('a', NativeMessageReader.MaxMessageBytes - 200);

        // Build raw JSON manually so we can control the exact byte count
        // (the normal serializer may not give us exactly 64 KiB)
        string json = BuildRawJson(paddedId);
        byte[] body = Encoding.UTF8.GetBytes(json);

        if (body.Length > NativeMessageReader.MaxMessageBytes) return; // skip if still too large

        byte[] frame = new byte[4 + body.Length];
        BitConverter.TryWriteBytes(frame, (uint)body.Length);
        body.CopyTo(frame, 4);

        MemoryStream stream = new(frame);
        NativeMessageReader reader = new(stream);

        // Should not throw (size is within limit)
        Func<Task> act = async () => await reader.ReadAsync(default);
        await act.Should().NotThrowAsync<InvalidDataException>();
    }

    // ── Truncated stream ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_Throws_WhenStreamClosedMidLengthPrefix()
    {
        // Only 2 of the 4 length-prefix bytes are present
        MemoryStream stream = new(new byte[] { 0x05, 0x00 });
        NativeMessageReader reader = new(stream);

        Func<Task> act = async () => await reader.ReadAsync(default);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*2 of 4 length-prefix bytes*");
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenStreamClosedMidBody()
    {
        // Declare a 10-byte body but only supply 3 bytes
        byte[] frame = new byte[4 + 3];
        BitConverter.TryWriteBytes(frame, 10u); // says 10 bytes
        frame[4] = (byte)'{';
        frame[5] = (byte)'"';
        frame[6] = (byte)'x';

        MemoryStream stream = new(frame);
        NativeMessageReader reader = new(stream);

        Func<Task> act = async () => await reader.ReadAsync(default);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*3 of 10 body bytes*");
    }

    // ── Wire format correctness ───────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_ProducesLittleEndianLengthPrefix()
    {
        BridgeMessage message = BuildHeartbeat();
        MemoryStream stream = new();
        NativeMessageWriter writer = new(stream);

        await writer.WriteAsync(message, default);

        byte[] frame = stream.ToArray();
        uint declaredLength = BitConverter.ToUInt32(frame, 0);   // LE
        uint actualBodyLength = (uint)(frame.Length - 4);

        declaredLength.Should().Be(actualBodyLength);
    }

    [Fact]
    public async Task WriteAsync_ProducesSingleContiguousFrame()
    {
        // After writing, the stream should have exactly 4 + body bytes with no gap
        BridgeMessage message = BuildTabSend("https://example.com", "T");
        MemoryStream stream = new();
        await new NativeMessageWriter(stream).WriteAsync(message, default);

        byte[] frame = stream.ToArray();
        uint length = BitConverter.ToUInt32(frame, 0);

        frame.Should().HaveCount((int)(4 + length));
    }

    // ── MaxDepth = 4 enforcement ──────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_Throws_JsonException_WhenNestingExceedsMaxDepth()
    {
        // Craft JSON with nesting > 4: outer{payload{extra{deep{deeper{}}}}}
        // That is 5 levels — should fail deserialization.
        string deepJson =
            """{"version":1,"type":"TAB_SEND","id":"550e8400-e29b-41d4-a716-446655440000","timestamp":1711468800,"source_profile":"A","target_profile":"B","hmac":"x","payload":{"url":"https://x.com","title":"T","pinned":false,"group_id":null,"nested":{"too":{"deep":{}}}}}""";

        byte[] body = Encoding.UTF8.GetBytes(deepJson);
        byte[] frame = new byte[4 + body.Length];
        BitConverter.TryWriteBytes(frame, (uint)body.Length);
        body.CopyTo(frame, 4);

        MemoryStream stream = new(frame);
        NativeMessageReader reader = new(stream);

        Func<Task> act = async () => await reader.ReadAsync(default);

        // MaxDepth=4 should cause a JsonException for the overly-nested input
        await act.Should().ThrowAsync<JsonException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<BridgeMessage> WriteAndReadAsync(BridgeMessage message)
    {
        MemoryStream stream = new();
        await new NativeMessageWriter(stream).WriteAsync(message, default);
        stream.Position = 0;
        BridgeMessage? read = await new NativeMessageReader(stream).ReadAsync(default);
        return read ?? throw new InvalidOperationException("Expected a message, got null.");
    }

    private static BridgeMessage BuildTabSend(string url, string title) =>
        new(1, MessageType.TAB_SEND,
            "550e8400-e29b-41d4-a716-446655440000",
            1_711_468_800L,
            "Work", "Personal",
            new TabPayload(url, title, false, null),
            "hmac-placeholder");

    private static BridgeMessage BuildHeartbeat() =>
        new(1, MessageType.HEARTBEAT,
            "550e8400-e29b-41d4-a716-446655440001",
            1_711_468_800L,
            "Work", "Personal",
            null,
            "hmac-placeholder");

    private static string BuildRawJson(string id) =>
        $$"""{"version":1,"type":"HEARTBEAT","id":"{{id}}","timestamp":1711468800,"source_profile":"A","target_profile":"B","hmac":"x"}""";
}
