namespace TabBridge.Host.Browser;

/// <summary>Registry of known Gecko-based browsers and runtime detection helpers.</summary>
public static class KnownBrowsers
{
    /// <summary>All supported Gecko-based browsers.</summary>
    public static readonly BrowserDescriptor[] All =
    [
        new("Firefox",   @"Software\Mozilla\NativeMessagingHosts",       @"Mozilla\Firefox", "Profile Groups"),
        new("Waterfox",  @"Software\Waterfox\NativeMessagingHosts",      "Waterfox",         "Profile Groups"),
        new("LibreWolf", @"Software\LibreWolf\NativeMessagingHosts",     "LibreWolf",        "Profile Groups"),
        new("Floorp",    @"Software\Ablaze\Floorp\NativeMessagingHosts", "Floorp",           "Profile Groups"),
        new("Zen",       @"Software\Mozilla\NativeMessagingHosts",       "zen",              "Profile Groups"),
    ];

    /// <summary>
    /// Detects the browser that launched this process by inspecting the parent process name.
    /// Falls back to the first installed browser, then to Firefox.
    /// </summary>
    public static BrowserDescriptor DetectFromParentProcess()
    {
        string parentExe = GetParentProcessExeName().ToLowerInvariant();
        foreach (BrowserDescriptor browser in All)
            if (parentExe.Contains(browser.Name, StringComparison.OrdinalIgnoreCase))
                return browser;
        foreach (BrowserDescriptor browser in All)
            if (Directory.Exists(browser.GetRoamingPath()))
                return browser;
        return All[0]; // fallback to Firefox
    }

    /// <summary>Returns all browsers that have a roaming AppData directory present.</summary>
    public static IEnumerable<BrowserDescriptor> DetectInstalled()
        => All.Where(b => Directory.Exists(b.GetRoamingPath()));

    private static string GetParentProcessExeName()
    {
        try
        {
            int parentPid = GetParentProcessId();
            using System.Diagnostics.Process parent = System.Diagnostics.Process.GetProcessById(parentPid);
            return Path.GetFileNameWithoutExtension(parent.MainModule?.FileName ?? string.Empty);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int GetParentProcessId()
    {
        // Uses NtQueryInformationProcess via interop – stub returns 0 until implemented
        return 0;
    }
}
