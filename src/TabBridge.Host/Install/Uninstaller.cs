using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TabBridge.Host.Browser;

namespace TabBridge.Host.Install;

/// <summary>Removes registry entries and optionally the app directory.</summary>
public static class Uninstaller
{
    private const string NmhName = "tab_bridge";
    private static readonly string AppDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "tab-bridge");

    /// <summary>Runs the uninstaller and returns 0 on success.</summary>
    public static int Run(ILoggerFactory loggerFactory)
    {
        ILogger logger = loggerFactory.CreateLogger(nameof(Uninstaller));

        UnregisterFromBrowsers(logger);
        logger.LogInformation("Uninstallation complete. App files remain in {AppDir}.", AppDir);
        return 0;
    }

    private static void UnregisterFromBrowsers(ILogger logger)
    {
        foreach (BrowserDescriptor browser in KnownBrowsers.All)
        {
            string regPath = browser.GetRegistryPath(NmhName);
            try
            {
                Registry.CurrentUser.DeleteSubKey(regPath, throwOnMissingSubKey: false);
                logger.LogInformation("Removed registry key HKCU\\{RegPath}", regPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to remove registry key HKCU\\{RegPath}", regPath);
            }
        }
    }
}
