using TabBridge.Host.Detection;

namespace TabBridge.Host.Tests;

public sealed class ProfileDetectorTests
{
    [Fact]
    public void PrefsParser_ReturnsNull_WhenFileDoesNotExist()
    {
        string? value = PrefsParser.ReadValue("nonexistent_prefs.js", "toolkit.profiles.storeID");

        value.Should().BeNull();
    }

    [Fact]
    public void PrefsParser_ReturnsValue_WhenKeyExists()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                user_pref("toolkit.profiles.storeID", "abc123");
                user_pref("other.key", "other.value");
                """);

            string? value = PrefsParser.ReadValue(tempFile, "toolkit.profiles.storeID");

            value.Should().Be("abc123");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void PrefsParser_ReturnsNull_WhenKeyNotPresent()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """user_pref("other.key", "value");""");

            string? value = PrefsParser.ReadValue(tempFile, "toolkit.profiles.storeID");

            value.Should().BeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
