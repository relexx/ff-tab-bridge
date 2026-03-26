using Microsoft.Extensions.Logging;

namespace TabBridge.Host.Broker;

/// <summary>
/// Triggers broker shutdown after a configurable idle period with no connected clients.
/// </summary>
public sealed class ShutdownTimer : IDisposable
{
    private readonly TimeSpan _idleTimeout;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private Timer? _timer;

    /// <param name="idleTimeout">Time with no clients before shutdown. Default: 60 seconds.</param>
    public ShutdownTimer(ILoggerFactory loggerFactory, CancellationTokenSource cts, TimeSpan? idleTimeout = null)
    {
        _idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(60);
        _logger = loggerFactory.CreateLogger(nameof(ShutdownTimer));
        _cts = cts;
    }

    /// <summary>Resets the idle timer. Call when the client count drops to zero.</summary>
    public void Reset()
    {
        _timer?.Dispose();
        _timer = new Timer(OnIdle, null, _idleTimeout, Timeout.InfiniteTimeSpan);
        _logger.LogDebug("Shutdown timer reset ({Timeout}s).", _idleTimeout.TotalSeconds);
    }

    /// <summary>Cancels the idle timer. Call when a new client connects.</summary>
    public void Cancel()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void OnIdle(object? _)
    {
        _logger.LogInformation("No clients for {Timeout}s – broker shutting down.", _idleTimeout.TotalSeconds);
        _cts.Cancel();
    }

    /// <inheritdoc/>
    public void Dispose() => _timer?.Dispose();
}
