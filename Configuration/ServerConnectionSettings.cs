namespace CompanyCLI.Configuration;

public enum DatabaseProvider
{
    SqlServer,
    MySql
}

public enum SqlAuthenticationMode
{
    Windows,
    SqlServer
}

public enum MySqlSslModeOption
{
    None,
    Preferred,
    Required,
    VerifyCA,
    VerifyFull
}

public sealed class ServerConnectionSettings
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    public string Server { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    public uint Port { get; set; } = 3306;

    public SqlAuthenticationMode AuthenticationMode { get; set; } = SqlAuthenticationMode.Windows;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool TrustServerCertificate { get; set; }

    public MySqlSslModeOption MySqlSslMode { get; set; } = MySqlSslModeOption.None;

    public int ConnectionTimeoutSeconds { get; set; } = 15;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Server) &&
        !string.IsNullOrWhiteSpace(Database) &&
        (Provider == DatabaseProvider.MySql
            ? Port is > 0 and <= 65535 &&
              !string.IsNullOrWhiteSpace(UserName) &&
              !string.IsNullOrWhiteSpace(Password)
            : AuthenticationMode == SqlAuthenticationMode.Windows ||
              (!string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password)));
}
