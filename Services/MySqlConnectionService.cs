using CompanyCLI.Configuration;
using MySqlConnector;

namespace CompanyCLI.Services;

public sealed class MySqlConnectionService : IDatabaseConnectionService
{
    public DatabaseProvider Provider => DatabaseProvider.MySql;

    public string BuildConnectionString(ServerConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Provider != DatabaseProvider.MySql)
        {
            throw new InvalidOperationException("The connection settings are not configured for MySQL.");
        }

        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException(
                "MySQL server, port, database, username, and password must be configured.");
        }

        var builder = new MySqlConnectionStringBuilder
        {
            Server = settings.Server.Trim(),
            Port = settings.Port,
            Database = settings.Database.Trim(),
            UserID = settings.UserName.Trim(),
            Password = settings.Password,
            ConnectionTimeout = (uint)Math.Clamp(settings.ConnectionTimeoutSeconds, 5, 120),
            SslMode = ToMySqlSslMode(settings.MySqlSslMode)
        };

        return builder.ConnectionString;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(
        ServerConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var connectionString = BuildConnectionString(settings);
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            stopwatch.Stop();
            return new ConnectionTestResult(
                true,
                $"MySQL connection successful. Server version: {connection.ServerVersion}",
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new ConnectionTestResult(false, "Connection test was cancelled.", stopwatch.Elapsed);
        }
        catch (MySqlException exception)
        {
            stopwatch.Stop();
            return new ConnectionTestResult(
                false,
                $"MySQL connection failed: {exception.Message}",
                stopwatch.Elapsed);
        }
        catch (InvalidOperationException exception)
        {
            stopwatch.Stop();
            return new ConnectionTestResult(false, exception.Message, stopwatch.Elapsed);
        }
    }

    private static MySqlSslMode ToMySqlSslMode(MySqlSslModeOption sslMode)
    {
        return sslMode switch
        {
            MySqlSslModeOption.None => MySqlSslMode.None,
            MySqlSslModeOption.Preferred => MySqlSslMode.Preferred,
            MySqlSslModeOption.Required => MySqlSslMode.Required,
            MySqlSslModeOption.VerifyCA => MySqlSslMode.VerifyCA,
            MySqlSslModeOption.VerifyFull => MySqlSslMode.VerifyFull,
            _ => MySqlSslMode.None
        };
    }
}
