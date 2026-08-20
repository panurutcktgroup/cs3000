using CompanyCLI.Configuration;

namespace CompanyCLI.Services;

public static class DatabaseConnectionServiceFactory
{
    public static IDatabaseConnectionService Create(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => new SqlServerConnectionService(),
            DatabaseProvider.MySql => new MySqlConnectionService(),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.")
        };
    }
}
