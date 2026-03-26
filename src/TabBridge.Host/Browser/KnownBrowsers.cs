using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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
    /// Falls back to the first installed browser (by AppData presence), then to Firefox.
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
        return All[0]; // final fallback to Firefox
    }

    /// <summary>Returns all browsers that have a roaming AppData directory present on this machine.</summary>
    public static IEnumerable<BrowserDescriptor> DetectInstalled()
        => All.Where(b => Directory.Exists(b.GetRoamingPath()));

    // ── Parent process detection ──────────────────────────────────────────────

    private static string GetParentProcessExeName()
    {
        try
        {
            int parentPid = GetParentProcessId();
            if (parentPid <= 0) return string.Empty;
            using Process parent = Process.GetProcessById(parentPid);
            return Path.GetFileNameWithoutExtension(parent.MainModule?.FileName ?? string.Empty);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Returns the PID of the parent process via <c>NtQueryInformationProcess</c>.
    /// Returns 0 on any failure so detection falls back to AppData presence.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static int GetParentProcessId()
    {
        ProcessBasicInformation pbi = default;
        try
        {
            int status = NtQueryInformationProcess(
                Process.GetCurrentProcess().Handle,
                0, // ProcessBasicInformation class
                ref pbi,
                Marshal.SizeOf<ProcessBasicInformation>(),
                out _);
            return status == 0 ? (int)pbi.InheritedFromUniqueProcessId : 0;
        }
        catch
        {
            return 0;
        }
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("ntdll.dll")]
    [SupportedOSPlatform("windows")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

    /// <summary>
    /// Layout matches <c>PROCESS_BASIC_INFORMATION</c> on 64-bit Windows.
    /// All fields are <see cref="IntPtr"/>-sized to correctly align with natural padding.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }
}
