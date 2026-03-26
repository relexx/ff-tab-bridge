using TabBridge.Host.Browser;

namespace TabBridge.Host.Detection;

/// <summary>
/// Fallback profile reader for browsers that do not use the Selectable Profile Service.
/// Reads <c>profiles.ini</c> to derive a profile name from the profile directory path.
/// </summary>
public static class LegacyProfileReader
{
    /// <summary>
    /// Returns a <see cref="ProfileInfo"/> derived from <c>profiles.ini</c> for <paramref name="ownProfilePath"/>.
    /// Profile name falls back to the directory name if no match is found.
    /// </summary>
    public static ProfileInfo Read(BrowserDescriptor browser, string ownProfilePath)
    {
        string iniPath = browser.GetProfilesIniPath();
        string profileDirName = Path.GetFileName(ownProfilePath.TrimEnd(Path.DirectorySeparatorChar));

        if (File.Exists(iniPath))
        {
            string? name = ParseNameFromIni(iniPath, profileDirName);
            if (name is not null)
                return new ProfileInfo(0, name, string.Empty, string.Empty, IsSelectableProfile: false);
        }

        return new ProfileInfo(0, profileDirName, string.Empty, string.Empty, IsSelectableProfile: false);
    }

    private static string? ParseNameFromIni(string iniPath, string profileDirName)
    {
        // Simple INI parser: find [ProfileN] section whose Path ends with profileDirName
        bool inMatchingSection = false;

        foreach (string line in File.ReadLines(iniPath))
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith('['))
            {
                inMatchingSection = false;
                continue;
            }

            if (inMatchingSection && trimmed.StartsWith("Name="))
                return trimmed["Name=".Length..];

            if (trimmed.StartsWith("Path=") && trimmed.EndsWith(profileDirName, StringComparison.OrdinalIgnoreCase))
                inMatchingSection = true;
        }

        return null;
    }
}
