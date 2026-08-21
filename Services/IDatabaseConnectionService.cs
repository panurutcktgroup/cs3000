using CompanyCLI.Configuration;

namespace CompanyCLI.Services;

public interface IDatabaseConnectionService
{
    DatabaseProvider Provider { get; }

    Task<ConnectionTestResult> TestConnectionAsync(
        ServerConnectionSettings settings,
        CancellationToken cancellationToken = default);
}
