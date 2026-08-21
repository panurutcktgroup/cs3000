using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace CompanyCLI.Services;

public sealed class LocalAuthenticationService
{
    private const string ApplicationFolderName = "CompanyCLI";
    private const string DatabaseFileName = "authentication.db";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 120_000;

    public string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationFolderName,
        DatabaseFileName);

    public async Task<bool> VerifyCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return false;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PasswordHash, PasswordSalt
            FROM Users
            WHERE Username = $username AND IsActive = 1;
            """;
        command.Parameters.AddWithValue("$username", username.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return false;

        var storedHash = (byte[])reader["PasswordHash"];
        var storedSalt = (byte[])reader["PasswordSalt"];
        var calculatedHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            storedSalt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return CryptographicOperations.FixedTimeEquals(calculatedHash, storedHash);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(DatabasePath)
            ?? throw new InvalidOperationException("Could not determine the authentication database directory.");
        Directory.CreateDirectory(directory);

        var connection = new SqliteConnection($"Data Source={DatabasePath}");
        try
        {
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task EnsureSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Users
                (
                    Username TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
                    PasswordHash BLOB NOT NULL,
                    PasswordSalt BLOB NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = """
            INSERT INTO Users (Username, PasswordHash, PasswordSalt, IsActive, CreatedAt)
            SELECT $username, $passwordHash, $passwordSalt, 1, $createdAt
            WHERE NOT EXISTS
            (
                SELECT 1 FROM Users WHERE Username = $username
            );
            """;

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            "1234",
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        seedCommand.Parameters.AddWithValue("$username", "admin");
        seedCommand.Parameters.AddWithValue("$passwordHash", hash);
        seedCommand.Parameters.AddWithValue("$passwordSalt", salt);
        seedCommand.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        await seedCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
