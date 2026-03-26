using Microsoft.Data.Sqlite;

namespace TabBridge.Host.Detection;

/// <summary>Reads profile information from the Selectable Profile Service SQLite database.</summary>
public static class SelectableProfileReader
{
    /// <summary>
    /// Queries <paramref name="dbPath"/> for the profile whose <c>path</c> column matches
    /// <paramref name="relativeProfilePath"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Profile not found in the database.</exception>
    public static async Task<ProfileInfo> ReadAsync(string dbPath, string relativeProfilePath, CancellationToken cancellationToken)
    {
        SqliteConnectionStringBuilder connBuilder = new()
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly // security rule #6
        };

        await using SqliteConnection conn = new(connBuilder.ToString());
        await conn.OpenAsync(cancellationToken);

        await ValidateSchemaAsync(conn, cancellationToken);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, avatar, themeBg FROM Profiles WHERE path = @path";
        cmd.Parameters.AddWithValue("@path", relativeProfilePath);

        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new ProfileInfo(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                IsSelectableProfile: true);
        }

        throw new InvalidOperationException($"Profile not found for path: {relativeProfilePath}");
    }

    /// <summary>Returns all profiles from <paramref name="dbPath"/>, ordered by name.</summary>
    public static async Task<IReadOnlyList<ProfileInfo>> ListAllAsync(string dbPath, CancellationToken cancellationToken)
    {
        SqliteConnectionStringBuilder connBuilder = new()
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly // security rule #6
        };

        await using SqliteConnection conn = new(connBuilder.ToString());
        await conn.OpenAsync(cancellationToken);

        await ValidateSchemaAsync(conn, cancellationToken);

        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, avatar, themeBg FROM Profiles ORDER BY name";

        List<ProfileInfo> profiles = [];
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(new ProfileInfo(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                IsSelectableProfile: true));
        }

        return profiles;
    }

    /// <summary>
    /// Validates that the expected Profiles table and columns exist.
    /// Throws if the schema is unexpected (undocumented schema may change between browser versions).
    /// </summary>
    private static async Task ValidateSchemaAsync(SqliteConnection conn, CancellationToken cancellationToken)
    {
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Profiles'";
        object? result = await cmd.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new InvalidOperationException("Selectable Profile DB does not contain a 'Profiles' table.");
    }
}
