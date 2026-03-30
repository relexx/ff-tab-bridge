using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using TabBridge.Host.Nmh;
using TabBridge.Host.Protocol;
using TabBridge.Host.Security;

namespace TabBridge.Host.Broker;

/// <summary>
/// Runs the broker mode: Named Pipe server that accepts NMH clients and routes messages between profiles.
/// Auto-shuts down 60 seconds after the last client disconnects.
/// </summary>
public static class BrokerMode
{
    /// <summary>Runs the broker until <paramref name="cancellationToken"/> is cancelled or the idle timer fires.</summary>
    public static async Task<int> RunAsync(ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger(nameof(BrokerMode));

        // Single-instance guard: only one broker per user session
        string userSid = WindowsIdentity.GetCurrent().User!.Value;
        string mutexName = $"Local\\tab-bridge-broker-{userSid}";
        using Mutex singleInstanceMutex = new(initiallyOwned: true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            logger.LogWarning("Broker is already running for this user. Exiting.");
            return 1;
        }

        string pipeName = PipeSecurityFactory.GetPipeName();
        logger.LogInformation("Broker starting on pipe: {PipeName}", pipeName);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using ShutdownTimer shutdownTimer = new(loggerFactory, linkedCts);

        PipeSecurity pipeSecurity = PipeSecurityFactory.CreateRestrictedPipeSecurity();
        HmacValidator hmac = new(LoadSecret());
        MessageValidator validator = new();
        ReplayGuard replayGuard = new();
        RateLimiter rateLimiter = new();

        ConcurrentDictionary<Guid, ClientSession> sessions = new();
        shutdownTimer.Reset(); // start idle timer before any client connects

        while (!linkedCts.IsCancellationRequested)
        {
            NamedPipeServerStream serverPipe = NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, // security rule #10
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity);

            try
            {
                await serverPipe.WaitForConnectionAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                await serverPipe.DisposeAsync();
                break;
            }

            ClientSession session = new(serverPipe);
            sessions[session.SessionId] = session;
            shutdownTimer.Cancel();
            logger.LogInformation("Client connected: {SessionId}", session.SessionId);

            _ = HandleClientAsync(session, sessions, hmac, validator, replayGuard, rateLimiter,
                shutdownTimer, loggerFactory, linkedCts.Token);
        }

        logger.LogInformation("Broker stopped.");
        return 0;
    }

    private static async Task HandleClientAsync(
        ClientSession session,
        ConcurrentDictionary<Guid, ClientSession> sessions,
        HmacValidator hmac,
        MessageValidator validator,
        ReplayGuard replayGuard,
        RateLimiter rateLimiter,
        ShutdownTimer shutdownTimer,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger(nameof(BrokerMode));

        try
        {
            NativeMessageReader reader = new(session.Pipe);
            NativeMessageWriter writer = new(session.Pipe);

            while (!cancellationToken.IsCancellationRequested)
            {
                BridgeMessage? message = await reader.ReadAsync(cancellationToken);
                if (message is null) break;

                if (!hmac.Validate(message))
                {
                    logger.LogWarning("[{Id}] HMAC validation failed – dropping message.", session.SessionId);
                    continue;
                }

                if (!replayGuard.TryAdd(message.Id, message.Timestamp))
                {
                    logger.LogWarning("[{Id}] Replay detected – dropping {MsgId}.", session.SessionId, message.Id);
                    continue;
                }

                if (!rateLimiter.TryConsume(session.RateLimiterKey))
                {
                    logger.LogWarning("[{Id}] Rate limit exceeded.", session.SessionId);
                    continue;
                }

                string? validationError = validator.Validate(message);
                if (validationError is not null)
                {
                    logger.LogWarning("[{Id}] Validation error: {Error}", session.SessionId, validationError);
                    continue;
                }

                await RouteMessageAsync(message, session, sessions, writer, hmac, logger, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[{Id}] Client error.", session.SessionId);
        }
        finally
        {
            sessions.TryRemove(session.SessionId, out _);
            await session.DisposeAsync();
            logger.LogInformation("Client disconnected: {SessionId}", session.SessionId);

            if (sessions.IsEmpty)
                shutdownTimer.Reset();
        }
    }

    private static async Task RouteMessageAsync(
        BridgeMessage message,
        ClientSession sender,
        ConcurrentDictionary<Guid, ClientSession> sessions,
        NativeMessageWriter replyWriter,
        HmacValidator hmac,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (message.Type == MessageType.REGISTER)
        {
            sender.ProfileName = message.SourceProfile;
            logger.LogInformation("[{Id}] Registered as profile '{Profile}'.", sender.SessionId, sender.ProfileName);

            BridgeMessage ack = BrokerMessageFactory.CreateAck(message, hmac);
            await replyWriter.WriteAsync(ack, cancellationToken);
            return;
        }

        if (message.Type == MessageType.PROFILE_LIST_REQUEST)
        {
            IEnumerable<string> otherProfiles = sessions.Values
                .Where(s => s.ProfileName is not null &&
                            !string.Equals(s.ProfileName, message.SourceProfile, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.ProfileName!);

            BridgeMessage response = BrokerMessageFactory.CreateProfileListResponse(message, otherProfiles, hmac);
            await replyWriter.WriteAsync(response, cancellationToken);
            return;
        }

        // Route to target profile
        ClientSession? target = sessions.Values.FirstOrDefault(s =>
            string.Equals(s.ProfileName, message.TargetProfile, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            logger.LogWarning("No client registered for target profile '{Target}'.", message.TargetProfile);
            return;
        }

        NativeMessageWriter targetWriter = new(target.Pipe);
        await targetWriter.WriteAsync(message, cancellationToken);
    }

    private static byte[] LoadSecret() => SecretLoader.Load();
}
