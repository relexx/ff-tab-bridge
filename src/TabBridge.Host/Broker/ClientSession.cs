using System.IO.Pipes;
using TabBridge.Host.Security;

namespace TabBridge.Host.Broker;

/// <summary>Holds per-client state for one connected NMH instance.</summary>
public sealed class ClientSession : IAsyncDisposable
{
    /// <summary>The Named Pipe stream for this client.</summary>
    public NamedPipeServerStream Pipe { get; }

    /// <summary>The registered profile name for this client, or <c>null</c> before REGISTER.</summary>
    public string? ProfileName { get; set; }

    /// <summary>Per-client rate limiter bucket reference (delegates to the shared <see cref="RateLimiter"/>).</summary>
    public string RateLimiterKey => ProfileName ?? Pipe.GetImpersonationUserName();

    /// <summary>Unique session identifier for logging.</summary>
    public Guid SessionId { get; } = Guid.NewGuid();

    /// <param name="pipe">The server-side pipe stream for this client.</param>
    public ClientSession(NamedPipeServerStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        Pipe = pipe;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Pipe.DisposeAsync();
    }
}
