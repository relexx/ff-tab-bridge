namespace TabBridge.Host.Browser;

/// <summary>
/// Describes a Gecko-based browser: its registry path for NMH registration,
/// its roaming AppData sub-path, and the name of the Profile Groups directory.
/// </summary>
public record BrowserDescriptor(
    string Name,
    string RegistrySubKey,
    string AppDataSubPath,
    string ProfileGroupsDir)
{
    /// <summary>Returns the full roaming AppData path for this browser.</summary>
    public string GetRoamingPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppDataSubPath);

    /// <summary>Returns the path to <c>profiles.ini</c> for this browser.</summary>
    public string GetProfilesIniPath() => Path.Combine(GetRoamingPath(), "profiles.ini");

    /// <summary>Returns the path to the Profile Groups directory for this browser.</summary>
    public string GetProfileGroupsPath() => Path.Combine(GetRoamingPath(), ProfileGroupsDir);

    /// <summary>Returns the full registry key path for the given NMH name.</summary>
    public string GetRegistryPath(string nmhName) => $@"{RegistrySubKey}\{nmhName}";
}
