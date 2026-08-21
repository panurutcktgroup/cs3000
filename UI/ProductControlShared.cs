using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Spectre.Console;
using CompanyCLI.Configuration;
using CompanyCLI.Services;
using Microsoft.Data.SqlClient;

namespace CompanyCLI.UI;

public static partial class ProductControlUI
{
    // --------------------
    // Helpers
    // --------------------
    private static string Truncate(string? s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= maxLen ? s : s.Substring(0, maxLen);
    }

    private static string BuildConnectionStringForSource(ServerConnectionSettings mainSettings, string selectedHost, int selectedPort, out ServerConnectionSettings usedSettings)
    {
        if (string.IsNullOrEmpty(selectedHost))
        {
            usedSettings = mainSettings;
            var connService = DatabaseConnectionServiceFactory.Create(mainSettings.Provider);
            if (connService is not SqlServerConnectionService)
                throw new InvalidOperationException("Expected SQL Server provider for Product Control.");
            var csMain = ((SqlServerConnectionService)connService).BuildConnectionString(mainSettings);
            var sbMain = new SqlConnectionStringBuilder(csMain) { ConnectTimeout = Math.Clamp(mainSettings.ConnectionTimeoutSeconds > 0 ? mainSettings.ConnectionTimeoutSeconds : DefaultConnectTimeoutSeconds, 5, 300) };
            return sbMain.ConnectionString;
        }
        else
        {
            var temp = new ServerConnectionSettings
            {
                Provider = mainSettings.Provider,
                Server = $"{selectedHost},{selectedPort}",
                Database = mainSettings.Database,
                AuthenticationMode = mainSettings.AuthenticationMode,
                UserName = mainSettings.UserName,
                Password = mainSettings.Password,
                TrustServerCertificate = mainSettings.TrustServerCertificate,
                ConnectionTimeoutSeconds = Math.Clamp(mainSettings.ConnectionTimeoutSeconds > 0 ? mainSettings.ConnectionTimeoutSeconds : DefaultConnectTimeoutSeconds, 5, 300),
                MySqlSslMode = mainSettings.MySqlSslMode
            };
            usedSettings = temp;
            var connService = DatabaseConnectionServiceFactory.Create(temp.Provider);
            if (connService is not SqlServerConnectionService)
                throw new InvalidOperationException("Expected SQL Server provider for Product Control.");
            var cs = ((SqlServerConnectionService)connService).BuildConnectionString(temp);
            var builder = new SqlConnectionStringBuilder(cs) { ConnectTimeout = Math.Clamp(temp.ConnectionTimeoutSeconds, 5, 300) };
            return builder.ConnectionString;
        }
    }

    private static bool TryTcpProbe(string connectionString, int probeTimeoutSeconds, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            var sb = new SqlConnectionStringBuilder(connectionString);
            var dataSource = (sb.DataSource ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(dataSource))
            {
                errorMessage = "Connection data source is empty.";
                return false;
            }

            if (dataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
                dataSource = dataSource.Substring(4).Trim();

            if (dataSource.Contains("\\"))
            {
                var hostPart = dataSource.Split('\\', 2)[0].Trim();
                if (string.IsNullOrEmpty(hostPart))
                {
                    errorMessage = "Invalid data source (named instance missing host).";
                    return false;
                }

                try
                {
                    var addrs = System.Net.Dns.GetHostAddresses(hostPart);
                    if (addrs == null || addrs.Length == 0)
                    {
                        errorMessage = $"Cannot resolve host '{hostPart}'.";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"DNS resolution failed for '{hostPart}': {ex.Message}";
                    return false;
                }

                // for named instances skip TCP port probe (SQL Browser dynamic port)
                return true;
            }

            string host = dataSource;
            int port = 1433;
            if (dataSource.Contains(","))
            {
                var parts = dataSource.Split(',', 2);
                host = parts[0].Trim();
                if (!int.TryParse(parts[1].Trim(), out port))
                    port = 1433;
            }

            if (string.IsNullOrEmpty(host))
            {
                errorMessage = "Invalid host in data source.";
                return false;
            }

            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(host, port);
            var completed = connectTask.Wait(TimeSpan.FromSeconds(Math.Max(1, probeTimeoutSeconds)));
            if (!completed || !tcp.Connected)
            {
                errorMessage = $"Cannot reach {host}:{port}. TCP connection failed or timed out.";
                return false;
            }

            return true;
        }
        catch (SocketException sex)
        {
            errorMessage = $"Network error during TCP probe: {sex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"TCP probe failed: {ex.Message}";
            return false;
        }
    }

    private static bool TrySelectSourceInternal(ServerConnectionSettings mainSettings, out string connectionString, out ServerConnectionSettings usedSettings)
    {
        connectionString = string.Empty;
        usedSettings = mainSettings;

        var profileStore = new LinkServerProfileStore();
        var profiles = profileStore.LoadAll();

        var prompt = new SelectionPrompt<string>().Title("Select data source:");
        prompt.AddChoice("Use configured server (default)");
        foreach (var p in profiles) prompt.AddChoice($"Use link profile: {p.Name} ({p.Host})");
        prompt.AddChoice("Ad-hoc IP (enter IP)");

        var choice = AnsiConsole.Prompt(prompt);

        string selectedHost = string.Empty;
        int selectedPort = 1433;
        if (choice.StartsWith("Use link profile:"))
        {
            var namePart = choice.Substring("Use link profile:".Length).Trim();
            var match = profiles.FirstOrDefault(x => namePart.StartsWith(x.Name, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(namePart, StringComparison.OrdinalIgnoreCase));
            if (match == null) match = profiles.FirstOrDefault(x => namePart.Contains(x.Name, StringComparison.OrdinalIgnoreCase));
            if (match == null) { TuiComponents.ShowError("Profile not found."); TuiComponents.Pause(); return false; }
            selectedHost = match.Host; selectedPort = match.Port;
        }
        else if (choice == "Ad-hoc IP (enter IP)")
        {
            var adh = AnsiConsole.Ask<string>("Enter IP or ip,port:").Trim();
            if (string.IsNullOrEmpty(adh)) return false;
            if (adh.Contains(","))
            {
                var parts = adh.Split(',', 2);
                selectedHost = parts[0].Trim();
                if (!int.TryParse(parts[1].Trim(), out selectedPort)) selectedPort = 1433;
            }
            else
            {
                selectedHost = adh;
                selectedPort = 1433;
            }
        }
        else
        {
            selectedHost = string.Empty;
        }

        try
        {
            connectionString = BuildConnectionStringForSource(mainSettings, selectedHost, selectedPort, out usedSettings);
            return true;
        }
        catch (Exception ex)
        {
            TuiComponents.ShowError($"Could not build connection string: {ex.Message}");
            TuiComponents.Pause();
            return false;
        }
    }
}
