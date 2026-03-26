using TabBridge.Host.Browser;

namespace TabBridge.Host.Detection;

/// <summary>Orchestrates three-stage profile detection for the current NMH instance.</summary>
public static class ProfileDetector
{
    /// <summary>
    /// Detects the current profile using a three-stage strategy:
    /// <list type="number">
    ///   <item>Read <c>toolkit.profiles.storeID</c> from <c>prefs.js</c></item>
    ///   <item>If found, query the Selectable Profile Service SQLite DB</item>
    ///   <item>Fall back to <c>profiles.ini</c> parsing</item>
    /// </list>
    /// </summary>
    public static async Task<ProfileInfo> DetectAsync(CancellationToken cancellationToken)
    {
        BrowserDescriptor browser = KnownBrowsers.DetectFromParentProcess();
        string ownProfilePath = ResolveOwnProfilePath();

        string? storeId = PrefsParser.ReadValue(
            Path.Combine(ownProfilePath, "prefs.js"),
            "toolkit.profiles.storeID");

        if (storeId is not null)
        {
            string dbPath = Path.Combine(browser.GetProfileGroupsPath(), $"{storeId}.sqlite");
            if (File.Exists(dbPath))
            {
                string relativeProfilePath = GetRelativeProfilePath(browser, ownProfilePath);
                return await SelectableProfileReader.ReadAsync(dbPath, relativeProfilePath, cancellationToken);
            }
        }

        return LegacyProfileReader.Read(browser, ownProfilePath);
    }

    /// <summary>Lists all selectable profiles for the detected browser.</summary>
    public static async Task<IReadOnlyList<ProfileInfo>> ListAllProfilesAsync(CancellationToken cancellationToken)
    {
        BrowserDescriptor browser = KnownBrowsers.DetectFromParentProcess();
        string ownProfilePath = ResolveOwnProfilePath();

        string? storeId = PrefsParser.ReadValue(
            Path.Combine(ownProfilePath, "prefs.js"),
            "toolkit.profiles.storeID");

        if (storeId is not null)
        {
            string dbPath = Path.Combine(browser.GetProfileGroupsPath(), $"{storeId}.sqlite");
            if (File.Exists(dbPath))
                return await SelectableProfileReader.ListAllAsync(dbPath, cancellationToken);
        }

        return [];
    }

    /// <summary>Resolves the profile directory of the current NMH process from its environment.</summary>
    private static string ResolveOwnProfilePath()
    {
        // Firefox passes MOZ_CRASHREPORTER_EVENTS_DIRECTORY or the profile is in the working directory
        // TODO: implement via environment variable inspection or parent process argument parsing
        string? profilePath = Environment.GetEnvironmentVariable("MOZ_PROFILE_PATH");
        if (!string.IsNullOrEmpty(profilePath) && Directory.Exists(profilePath))
            return profilePath;

        throw new InvalidOperationException("Cannot determine own profile path. Set MOZ_PROFILE_PATH.");
    }

    private static string GetRelativeProfilePath(BrowserDescriptor browser, string ownProfilePath)
    {
        string profilesRoot = Path.Combine(browser.GetRoamingPath(), "Profiles");
        if (ownProfilePath.StartsWith(profilesRoot, StringComparison.OrdinalIgnoreCase))
            return ownProfilePath[(profilesRoot.Length + 1)..];
        return ownProfilePath;
    }
}

/// <summary>Profile information returned by the detection pipeline.</summary>
public record ProfileInfo(
    int Id,
    string Name,
    string Avatar,
    string ThemeColor,
    bool IsSelectableProfile);
