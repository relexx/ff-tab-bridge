using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TabBridge.Host.Browser;
using TabBridge.Host.Security;

namespace TabBridge.Host.Install;

/// <summary>Prints a diagnostic report: installation state, secret presence, broker reachability.</summary>
public static class StatusCheck
{
    private const string NmhName = "tab_bridge";
    private static readonly string AppDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "tab-bridge");

    /// <summary>Runs the status check and returns 0 on success.</summary>
    public static int Run(ILoggerFactory loggerFactory)
    {
        ILogger logger = loggerFactory.CreateLogger(nameof(StatusCheck));

        CheckAppDirectory(logger);
        CheckSecret(logger);
        CheckNmhManifest(logger);
        CheckBrowserRegistrations(logger);
        CheckBrokerReachability(logger);

        return 0;
    }

    private static void CheckAppDirectory(ILogger logger)
    {
        bool exists = Directory.Exists(AppDir);
        logger.LogInformation("App directory: {AppDir} [{Status}]", AppDir, exists ? "OK" : "MISSING");
    }

    private static void CheckSecret(ILogger logger)
    {
        string secretPath = Path.Combine(AppDir, "secret.key");
        bool exists = File.Exists(secretPath);
        long size = exists ? new FileInfo(secretPath).Length : 0;
        logger.LogInformation("HMAC secret: {Path} [{Status}]", secretPath,
            exists && size == 32 ? "OK (32 bytes)" : exists ? $"WRONG SIZE ({size} bytes)" : "MISSING");
    }

    private static void CheckNmhManifest(ILogger logger)
    {
        string manifestPath = Path.Combine(AppDir, "tab_bridge.json");
        logger.LogInformation("NMH manifest: {Path} [{Status}]", manifestPath,
            File.Exists(manifestPath) ? "OK" : "MISSING");
    }

    private static void CheckBrowserRegistrations(ILogger logger)
    {
        foreach (BrowserDescriptor browser in KnownBrowsers.All)
        {
            string regPath = browser.GetRegistryPath(NmhName);
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(regPath);
            string status = key?.GetValue("") is string val && File.Exists(val) ? "OK" : "NOT REGISTERED";
            logger.LogInformation("Browser {Name}: HKCU\\{RegPath} [{Status}]", browser.Name, regPath, status);
        }
    }

    private static void CheckBrokerReachability(ILogger logger)
    {
        string pipeName = PipeSecurityFactory.GetPipeName();
        bool reachable = false;

        try
        {
            using NamedPipeClientStream pipe = new(".", pipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(500);
            reachable = true;
        }
        catch { }

        logger.LogInformation("Broker pipe: {PipeName} [{Status}]", pipeName, reachable ? "RUNNING" : "NOT RUNNING");
    }
}
