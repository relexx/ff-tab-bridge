using TabBridge.Host.Browser;

namespace TabBridge.Host.Tests;

public sealed class BrowserDescriptorTests
{
    [Fact]
    public void GetRoamingPath_CombinesAppDataWithSubPath()
    {
        BrowserDescriptor descriptor = new("Test", @"Software\Test", @"Test\App", "Groups");
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Test\App");

        descriptor.GetRoamingPath().Should().Be(expected);
    }

    [Fact]
    public void GetProfilesIniPath_IsInsideRoamingPathAndEndsWithIni()
    {
        BrowserDescriptor descriptor = new("Test", @"Software\Test", @"Test\App", "Groups");

        string iniPath = descriptor.GetProfilesIniPath();

        iniPath.Should().StartWith(descriptor.GetRoamingPath());
        iniPath.Should().EndWith("profiles.ini");
    }

    [Fact]
    public void GetProfileGroupsPath_IsInsideRoamingPathAndEndsWithGroupsDir()
    {
        BrowserDescriptor descriptor = new("Test", @"Software\Test", @"Test\App", "My Groups");

        string groupsPath = descriptor.GetProfileGroupsPath();

        groupsPath.Should().StartWith(descriptor.GetRoamingPath());
        groupsPath.Should().EndWith("My Groups");
    }

    [Fact]
    public void GetRegistryPath_CombinesSubKeyWithNmhName()
    {
        BrowserDescriptor descriptor = new("Firefox", @"Software\Mozilla\NativeMessagingHosts", @"Mozilla\Firefox", "Profile Groups");

        descriptor.GetRegistryPath("tab_bridge")
            .Should().Be(@"Software\Mozilla\NativeMessagingHosts\tab_bridge");
    }

    [Fact]
    public void GetRegistryPath_UsesProvidedNmhName()
    {
        BrowserDescriptor descriptor = new("Test", @"Software\Test\NMH", @"Test\App", "Groups");

        descriptor.GetRegistryPath("my_host").Should().EndWith(@"\my_host");
    }
}

public sealed class KnownBrowsersTests
{
    [Fact]
    public void All_ContainsExactlyFiveBrowsers()
    {
        KnownBrowsers.All.Length.Should().Be(5);
    }

    [Fact]
    public void All_ContainsAllExpectedBrowsersByName()
    {
        KnownBrowsers.All.Select(b => b.Name)
            .Should().BeEquivalentTo(["Firefox", "Waterfox", "LibreWolf", "Floorp", "Zen"]);
    }

    [Fact]
    public void All_WaterfoxHasCorrectRegistrySubKey()
    {
        BrowserDescriptor waterfox = KnownBrowsers.All.Single(b => b.Name == "Waterfox");

        waterfox.RegistrySubKey.Should().Be(@"Software\Waterfox\NativeMessagingHosts");
    }

    [Fact]
    public void All_LibreWolfHasItsOwnRegistryKey()
    {
        BrowserDescriptor libreWolf = KnownBrowsers.All.Single(b => b.Name == "LibreWolf");

        libreWolf.RegistrySubKey.Should().Be(@"Software\LibreWolf\NativeMessagingHosts");
    }

    [Fact]
    public void All_BrowsersHaveUniqueAppDataPaths()
    {
        // Zen reuses Mozilla registry key but must have a distinct AppData path
        KnownBrowsers.All.Select(b => b.AppDataSubPath)
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void DetectFromParentProcess_AlwaysReturnsNonNull()
    {
        // Falls back through parent process name → installed browser → All[0]
        BrowserDescriptor result = KnownBrowsers.DetectFromParentProcess();

        result.Should().NotBeNull();
    }

    [Fact]
    public void DetectFromParentProcess_ReturnsMemberOfAll()
    {
        BrowserDescriptor result = KnownBrowsers.DetectFromParentProcess();

        KnownBrowsers.All.Should().Contain(result);
    }

    [Fact]
    public void DetectInstalled_IsSubsetOfAll()
    {
        IEnumerable<BrowserDescriptor> installed = KnownBrowsers.DetectInstalled();

        installed.Should().BeSubsetOf(KnownBrowsers.All);
    }

    [Fact]
    public void DetectInstalled_ContainsNoDuplicates()
    {
        IEnumerable<BrowserDescriptor> installed = KnownBrowsers.DetectInstalled();

        installed.Should().OnlyHaveUniqueItems();
    }
}
