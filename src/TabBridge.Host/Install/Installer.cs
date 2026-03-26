using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TabBridge.Host.Browser;

namespace TabBridge.Host.Install;

/// <summary>Performs one-time installation: writes the NMH manifest, generates the HMAC secret, and registers with all detected browsers.</summary>
public static class Installer
{
    private const string NmhName = "tab_bridge";
    private static readonly string AppDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "tab-bridge");

    /// <summary>Runs the installer and returns 0 on success.</summary>
    public static int Run(ILoggerFactory loggerFactory)
    {
        ILogger logger = loggerFactory.CreateLogger(nameof(Installer));

        Directory.CreateDirectory(AppDir);
        Directory.CreateDirectory(Path.Combine(AppDir, "logs"));

        GenerateSecretIfMissing(logger);
        WriteNmhManifest(logger);
        RegisterWithBrowsers(logger);

        logger.LogInformation("Installation complete. App directory: {AppDir}", AppDir);
        return 0;
    }

    private static void GenerateSecretIfMissing(ILogger logger)
    {
        string secretPath = Path.Combine(AppDir, "secret.key");
        if (File.Exists(secretPath))
        {
            logger.LogInformation("Secret key already exists – skipping generation.");
            return;
        }

        byte[] secret = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(secretPath, secret);

        // Restrict ACL to owner only (File.SetAttributes does not suffice – full ACL needed in real impl)
        logger.LogInformation("Generated new 256-bit HMAC secret at {Path}", secretPath);
    }

    private static void WriteNmhManifest(ILogger logger)
    {
        string exePath = Path.Combine(AppDir, "tab-bridge.exe");
        string manifestPath = Path.Combine(AppDir, "tab_bridge.json");

        var manifest = new
        {
            name = NmhName,
            description = "Tab Bridge – Cross-Profile Tab Transfer",
            path = exePath,
            type = "stdio",
            allowed_extensions = new[] { "tab-bridge@relexx.de" }
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        logger.LogInformation("NMH manifest written to {Path}", manifestPath);
    }

    private static void RegisterWithBrowsers(ILogger logger)
    {
        string manifestPath = Path.Combine(AppDir, "tab_bridge.json");

        foreach (BrowserDescriptor browser in KnownBrowsers.DetectInstalled())
        {
            string regPath = browser.GetRegistryPath(NmhName);
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(regPath);
            key.SetValue("", manifestPath);
            logger.LogInformation("Registered NMH with {Browser} at HKCU\\{RegPath}", browser.Name, regPath);
        }
    }
}
