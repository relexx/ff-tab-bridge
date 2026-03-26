using System.Text.RegularExpressions;

namespace TabBridge.Host.Detection;

/// <summary>Extracts key-value pairs from a Firefox <c>prefs.js</c> file.</summary>
public static partial class PrefsParser
{
    // Matches: user_pref("key", "value"); or user_pref("key", 123);
    [GeneratedRegex(@"user_pref\(""(?<key>[^""]+)"",\s*""(?<val>[^""]*)""\s*\);", RegexOptions.Compiled)]
    private static partial Regex StringPrefPattern();

    /// <summary>
    /// Reads <paramref name="prefsPath"/> and returns the string value for <paramref name="key"/>,
    /// or <c>null</c> if not found.
    /// </summary>
    public static string? ReadValue(string prefsPath, string key)
    {
        if (!File.Exists(prefsPath)) return null;

        foreach (string line in File.ReadLines(prefsPath))
        {
            Match match = StringPrefPattern().Match(line);
            if (match.Success && match.Groups["key"].Value == key)
                return match.Groups["val"].Value;
        }

        return null;
    }
}
