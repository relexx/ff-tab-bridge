using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using TabBridge.Host.Protocol;
using TabBridge.Host.Security;

namespace TabBridge.Host.Nmh;

/// <summary>
/// Runs the NMH mode: bridges stdin/stdout (browser NMH protocol) to the broker via Named Pipe.
/// If the broker is not running, starts it first.
/// </summary>
public static class NmhMode
{
    /// <summary>Runs the NMH bridge until the browser closes stdin or cancellation is requested.</summary>
    public static async Task<int> RunAsync(ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger(nameof(NmhMode));

        string pipeName = PipeSecurityFactory.GetPipeName();
        logger.LogInformation("NMH mode starting. Broker pipe: {PipeName}", pipeName);

        await EnsureBrokerRunningAsync(logger, cancellationToken);

        await using NamedPipeClientStream pipe = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly); // security rule #10

        await pipe.ConnectAsync(5000, cancellationToken);
        logger.LogInformation("Connected to broker.");

        NativeMessageReader stdinReader = new(Console.OpenStandardInput());
        NativeMessageWriter stdoutWriter = new(Console.OpenStandardOutput());
        NativeMessageReader pipeReader = new(pipe);
        NativeMessageWriter pipeWriter = new(pipe);

        Task browserTobroker = ForwardAsync(stdinReader, pipeWriter, "browser→broker", logger, cancellationToken);
        Task brokerToBrowser = ForwardAsync(pipeReader, stdoutWriter, "broker→browser", logger, cancellationToken);

        await Task.WhenAny(browserTobroker, brokerToBrowser);
        logger.LogInformation("NMH bridge terminated.");
        return 0;
    }

    private static async Task ForwardAsync(
        NativeMessageReader reader,
        NativeMessageWriter writer,
        string direction,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                BridgeMessage? message = await reader.ReadAsync(cancellationToken);
                if (message is null) break;
                await writer.WriteAsync(message, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Forward {Direction} error", direction);
        }
    }

    private static async Task EnsureBrokerRunningAsync(ILogger logger, CancellationToken cancellationToken)
    {
        // TODO: check if broker pipe exists; if not, start tab-bridge.exe --broker
        await Task.CompletedTask;
    }
}
