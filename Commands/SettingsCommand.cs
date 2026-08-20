using CompanyCLI.Configuration;
using CompanyCLI.Services;
using CompanyCLI.UI;
using Spectre.Console;
using System.Security.Cryptography;

namespace CompanyCLI.Commands;

public static class SettingsCommand
{
    private static readonly ServerConnectionSettingsStore SettingsStore = new();
    private static string? _loadWarning;
    private static ServerConnectionSettings _settings = LoadSettings();
    private static bool? _lastConnectionSucceeded;

    public static string DatabaseConnectionStatus =>
        !_settings.IsConfigured
            ? "[red]○ Not configured[/]"
            : _lastConnectionSucceeded is true
                ? "[green]● Connected[/]"
                : _lastConnectionSucceeded is false
                    ? "[red]● Disconnected[/]"
                    : "[yellow]○ Not tested[/]";

    public static async Task TestConnectionAutomaticallyAsync()
    {
        if (!_settings.IsConfigured)
        {
            return;
        }

        var connectionService = DatabaseConnectionServiceFactory.Create(_settings.Provider);
        ConnectionTestResult? result = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Checking database connection...", async _ =>
            {
                result = await connectionService.TestConnectionAsync(_settings);
            });

        _lastConnectionSucceeded = result?.Success ?? false;
    }

    public static async Task RunAsync()
    {
        while (true)
        {
            Console.Clear();
            TuiComponents.ShowPageHeader(
                "SETTINGS",
                "Dashboard > Settings",
                "Configure and test a SQL Server or MySQL connection"
            );

            if (_loadWarning is not null)
            {
                TuiComponents.ShowError(_loadWarning);
                TuiComponents.Pause("Press ENTER to continue and configure the settings again...");
                _loadWarning = null;
                continue;
            }

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Settings Menu[/]")
                    .AddChoices(
                        "View Connection Settings",
                        "Configure Connection",
                        "Test Connection",
                        "Reset Connection Settings",
                        "Back"
                    )
            );

            switch (choice)
            {
                case "View Connection Settings":
                    ShowConnectionSettings();
                    break;

                case "Configure Connection":
                    ConfigureConnection();
                    break;

                case "Test Connection":
                    await TestConnectionAsync();
                    break;

                case "Reset Connection Settings":
                    ResetConnectionSettings();
                    break;

                case "Back":
                    return;
            }
        }
    }

    private static void ShowConnectionSettings()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "CONNECTION SETTINGS",
            "Dashboard > Settings > View",
            "Passwords are never displayed"
        );

        var table = new Table
        {
            Border = TableBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Title = new TableTitle("[bold cyan]Database Configuration[/]"),
            Expand = true
        };

        table.AddColumn("[grey]Setting[/]");
        table.AddColumn("[grey]Value[/]");

        table.AddRow("Status", _settings.IsConfigured ? "[green]Configured[/]" : "[yellow]Not configured[/]");
        table.AddRow("Provider", _settings.Provider.ToString());
        table.AddRow("Server", ValueOrDash(_settings.Server));
        table.AddRow("Port", _settings.Provider == DatabaseProvider.MySql
            ? _settings.Port.ToString()
            : "[grey]Included in server value[/]");
        table.AddRow("Database", ValueOrDash(_settings.Database));
        table.AddRow("Authentication", _settings.Provider == DatabaseProvider.MySql
            ? "Username / Password"
            : _settings.AuthenticationMode.ToString());
        table.AddRow("Username", _settings.Provider == DatabaseProvider.MySql ||
            _settings.AuthenticationMode == SqlAuthenticationMode.SqlServer
            ? ValueOrDash(_settings.UserName)
            : "[grey]Not applicable[/]");
        table.AddRow("Password", _settings.Provider == DatabaseProvider.MySql ||
            _settings.AuthenticationMode == SqlAuthenticationMode.SqlServer
            ? "[green]Configured (hidden)[/]"
            : "[grey]Not applicable[/]");
        table.AddRow("Trust server certificate", _settings.Provider == DatabaseProvider.SqlServer
            ? (_settings.TrustServerCertificate ? "[yellow]Yes[/]" : "[green]No[/]")
            : "[grey]Not applicable[/]");
        table.AddRow("MySQL SSL mode", _settings.Provider == DatabaseProvider.MySql
            ? _settings.MySqlSslMode.ToString()
            : "[grey]Not applicable[/]");
        table.AddRow("Connection timeout", $"{_settings.ConnectionTimeoutSeconds} seconds");
        table.AddRow("Storage", "[green]Encrypted for current Windows user[/]");

        AnsiConsole.Write(table);
        TuiComponents.Pause();
    }

    private static void ConfigureConnection()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "CONFIGURE CONNECTION",
            "Dashboard > Settings > Configure",
            "Choose a provider and enter its connection details"
        );

        var settings = new ServerConnectionSettings
        {
            Provider = AnsiConsole.Prompt(
                new SelectionPrompt<DatabaseProvider>()
                    .Title("Database [cyan]provider[/]:")
                    .AddChoices(DatabaseProvider.SqlServer, DatabaseProvider.MySql)
            )
        };

        settings.Server = AskRequired(
            settings.Provider == DatabaseProvider.MySql
                ? "MySQL [cyan]host[/] (e.g. 127.0.0.1):"
                : "SQL Server [cyan]server[/] (e.g. localhost or server,1433):");
        settings.Database = AskRequired("Database [cyan]name[/]:");

        if (settings.Provider == DatabaseProvider.MySql)
        {
            settings.Port = (uint)Math.Clamp(
                AnsiConsole.Ask<int>("MySQL port:", 3306),
                1,
                65535);
            settings.UserName = AskRequired("MySQL username:");
            settings.Password = AskSecretRequired("MySQL password:");
            settings.MySqlSslMode = AnsiConsole.Prompt(
                new SelectionPrompt<MySqlSslModeOption>()
                    .Title("MySQL [cyan]SSL mode[/]:")
                    .AddChoices(
                        MySqlSslModeOption.None,
                        MySqlSslModeOption.Preferred,
                        MySqlSslModeOption.Required,
                        MySqlSslModeOption.VerifyCA,
                        MySqlSslModeOption.VerifyFull)
            );
        }
        else
        {
            settings.AuthenticationMode = AnsiConsole.Prompt(
                new SelectionPrompt<SqlAuthenticationMode>()
                    .Title("SQL Server authentication [cyan]mode[/]:")
                    .AddChoices(SqlAuthenticationMode.Windows, SqlAuthenticationMode.SqlServer)
            );

            if (settings.AuthenticationMode == SqlAuthenticationMode.SqlServer)
            {
                settings.UserName = AskRequired("SQL Server username:");
                settings.Password = AskSecretRequired("SQL Server password:");
            }

            settings.TrustServerCertificate = AnsiConsole.Confirm(
                "Trust the SQL Server certificate?",
                false
            );
        }

        settings.ConnectionTimeoutSeconds = Math.Clamp(
            AnsiConsole.Ask<int>("Connection timeout in seconds (5-120):", 15),
            5,
            120
        );

        try
        {
            SettingsStore.Save(settings);
            _settings = settings;
            _lastConnectionSucceeded = null;
            TuiComponents.ShowSuccess($"{settings.Provider} connection settings saved securely.");
        }
        catch (CryptographicException exception)
        {
            TuiComponents.ShowError($"Could not encrypt settings: {exception.Message}");
        }
        catch (IOException exception)
        {
            TuiComponents.ShowError($"Could not save settings: {exception.Message}");
        }
        catch (PlatformNotSupportedException exception)
        {
            TuiComponents.ShowError(exception.Message);
        }

        TuiComponents.Pause();
    }

    private static void ResetConnectionSettings()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "RESET CONNECTION SETTINGS",
            "Dashboard > Settings > Reset",
            "This removes the encrypted settings file from this user profile"
        );

        if (!AnsiConsole.Confirm("Remove the saved connection settings?", false))
        {
            TuiComponents.ShowError("Reset operation cancelled.");
            TuiComponents.Pause();
            return;
        }

        try
        {
            SettingsStore.Clear();
            _settings = new ServerConnectionSettings();
            _lastConnectionSucceeded = null;
            TuiComponents.ShowSuccess("Saved connection settings removed.");
        }
        catch (IOException exception)
        {
            TuiComponents.ShowError($"Could not remove settings: {exception.Message}");
        }

        TuiComponents.Pause();
    }

    private static async Task TestConnectionAsync()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "TEST CONNECTION",
            "Dashboard > Settings > Test",
            $"A real connection attempt will be made to {_settings.Provider}"
        );

        if (!_settings.IsConfigured)
        {
            TuiComponents.ShowError("Connection is not configured. Configure it before testing.");
            TuiComponents.Pause();
            return;
        }

        ConnectionTestResult? result = null;
        var connectionService = DatabaseConnectionServiceFactory.Create(_settings.Provider);
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Testing {_settings.Provider} connection...", async _ =>
            {
                result = await connectionService.TestConnectionAsync(_settings);
            });

        result ??= new ConnectionTestResult(false, "Connection test did not return a result.", TimeSpan.Zero);
        _lastConnectionSucceeded = result.Success;
        if (result.Success)
        {
            TuiComponents.ShowSuccess(
                $"{result.Message} ({result.Duration.TotalMilliseconds:0} ms)"
            );
        }

        else
        {
            TuiComponents.ShowError(
                $"{result.Message} ({result.Duration.TotalMilliseconds:0} ms)"
            );
        }

        TuiComponents.Pause();
    }

    private static string AskSecretRequired(string prompt)
    {
        while (true)
        {
            var value = AnsiConsole.Prompt(new TextPrompt<string>(prompt).Secret());
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            TuiComponents.ShowError("This value is required.");
        }
    }

    private static string AskRequired(string prompt)
    {
        while (true)
        {
            var value = AnsiConsole.Ask<string>(prompt).Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            TuiComponents.ShowError("This value is required.");
        }
    }

    private static string ValueOrDash(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "[grey]-[/]"
            : Markup.Escape(value);
    }

    private static ServerConnectionSettings LoadSettings()
    {
        var store = new ServerConnectionSettingsStore();
        var settings = store.Load(out var errorMessage);
        _loadWarning = errorMessage;
        return settings;
    }

}
