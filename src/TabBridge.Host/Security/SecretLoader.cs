namespace TabBridge.Host.Security;

/// <summary>
/// Loads the HMAC shared secret from the application data directory.
/// The secret is a 32-byte (256-bit) random value generated during <c>--install</c>.
/// </summary>
public static class SecretLoader
{
    /// <summary>Path to the secret key file (readable by tests and the installer).</summary>
    internal static readonly string SecretPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "tab-bridge",
        "secret.key");

    /// <summary>
    /// Reads and returns the 32-byte HMAC secret from
    /// <c>%LOCALAPPDATA%\tab-bridge\secret.key</c>.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// Secret file does not exist. Run <c>tab-bridge.exe --install</c> first.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// File exists but is not exactly 32 bytes, indicating corruption or a manual edit.
    /// </exception>
    public static byte[] Load()
    {
        if (!File.Exists(SecretPath))
            throw new FileNotFoundException(
                $"HMAC secret not found at '{SecretPath}'. Run 'tab-bridge.exe --install' first.",
                SecretPath);

        byte[] secret = File.ReadAllBytes(SecretPath);

        if (secret.Length != 32)
            throw new InvalidDataException(
                $"Secret key is {secret.Length} bytes; expected exactly 32. " +
                "Re-run '--install' to regenerate.");

        return secret;
    }
}
