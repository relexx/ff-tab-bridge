using Microsoft.Extensions.Logging;
using TabBridge.Host.Broker;
using TabBridge.Host.Install;
using TabBridge.Host.Nmh;

namespace TabBridge.Host;

/// <summary>CLI entry point. Routes execution based on the first argument.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        ILogger logger = loggerFactory.CreateLogger("TabBridge");

        string mode = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            return mode switch
            {
                "--nmh"       => await NmhMode.RunAsync(loggerFactory, cts.Token),
                "--broker"    => await BrokerMode.RunAsync(loggerFactory, cts.Token),
                "--install"   => Installer.Run(loggerFactory),
                "--uninstall" => Uninstaller.Run(loggerFactory),
                "--status"    => StatusCheck.Run(loggerFactory),
                _             => PrintUsage()
            };
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Unhandled exception");
            return 1;
        }
    }

    private static int PrintUsage()
    {
        Console.Error.WriteLine("Usage: tab-bridge.exe --nmh | --broker | --install | --uninstall | --status");
        return 1;
    }
}
