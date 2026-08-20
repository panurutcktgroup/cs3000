using CompanyCLI.Configuration;
using Microsoft.Data.SqlClient;

namespace CompanyCLI.Services;

public sealed record ConnectionTestResult(bool Success, string Message, TimeSpan Duration);

public sealed class SqlServerConnectionService : IDatabaseConnectionService
{
    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    public string BuildConnectionString(ServerConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Provider != DatabaseProvider.SqlServer)
        {
            throw new InvalidOperationException("The connection settings are not configured for SQL Server.");
        }

        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException(
                "Server, database, and authentication credentials must be configured.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Server.Trim(),
            InitialCatalog = settings.Database.Trim(),
            IntegratedSecurity = settings.AuthenticationMode == SqlAuthenticationMode.Windows,
            TrustServerCertificate = settings.TrustServerCertificate,
            Encrypt = true,
            ConnectTimeout = Math.Clamp(settings.ConnectionTimeoutSeconds, 5, 120),
            ApplicationName = "CompanyCLI"
        };

        if (settings.AuthenticationMode == SqlAuthenticationMode.SqlServer)
        {
            builder.UserID = settings.UserName.Trim();
            builder.Password = settings.Password;
        }

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
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            stopwatch.Stop();
            return new ConnectionTestResult(
                true,
                $"Connection successful. Server version: {connection.ServerVersion}",
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new ConnectionTestResult(false, "Connection test was cancelled.", stopwatch.Elapsed);
        }
        catch (SqlException exception)
        {
            stopwatch.Stop();
            return new ConnectionTestResult(
                false,
                $"SQL Server connection failed: {exception.Message}",
                stopwatch.Elapsed);
        }
        catch (InvalidOperationException exception)
        {
            stopwatch.Stop();
            return new ConnectionTestResult(false, exception.Message, stopwatch.Elapsed);
        }
    }
}
