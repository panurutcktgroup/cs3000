using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Spectre.Console;
using CompanyCLI.Services;
using CompanyCLI.Configuration;
using Microsoft.Data.SqlClient;

namespace CompanyCLI.UI;

public static class MachineDataUI
{
    private const int DefaultPageSize = 30;
    private const int MaxPageSize = 200;
    private const int DefaultConnectTimeoutSeconds = 60;
    private const int DefaultCommandTimeoutSeconds = 120;

    public static async Task ShowAsync()
    {
        while (true)
        {
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Machine Data[/]")
                    .AddChoices(new[] {
                        "Machine Search",
                        "Machine Lists",
                        "Back"
                    }));

            if (action == "Back") break;

            try
            {
                if (action == "Machine Search")
                {
                    await MachineSearchAsync();
                }
                else if (action == "Machine Lists")
                {
                    await MachineListsAsync();
                }
            }
            catch (Exception ex)
            {
                TuiComponents.ShowError($"Error: {ex.Message}");
            }
        }
    }

    // Machine Search: prompt a search term (partial match on mcName)
    private static async Task MachineSearchAsync()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader("MACHINE SEARCH", "Machine Data > Machine Search", "Search by mcName (partial)");

        var q = AnsiConsole.Ask<string>("Enter mcName or partial (e.g. ABC):").Trim();
        if (string.IsNullOrEmpty(q))
        {
            TuiComponents.ShowError("Search term required.");
            TuiComponents.Pause();
            return;
        }

        // select source (configured, link profile, ad-hoc)
        if (!TrySelectSource(out var connectionString, out var dbName)) return;

        var targetTable = string.IsNullOrEmpty(dbName) ? "[dbo].[Master_mcName]" : $"[{dbName}].[dbo].[Master_mcName]";

        // build sql
        var sql = $@"
SELECT TOP (200) [mcName], [Factory], [Process], [DT_Update]
FROM {targetTable}
WHERE [mcName] LIKE @q
ORDER BY [DT_Update] DESC, [mcName] ASC;";

        // prepare connection string with increased timeout
        var sb = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = Math.Clamp(DefaultConnectTimeoutSeconds, 5, 300) };
        var connStr = sb.ConnectionString;

        if (!TryTcpProbe(connStr, 5, out var probeError))
        {
            TuiComponents.ShowError(probeError ?? "TCP probe failed.");
            TuiComponents.Pause();
            return;
        }

        var rows = new List<(string mcName, string Factory, string Process, string DT_Update)>();

        await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Searching machines...", async _ =>
        {
            await Task.Run(() =>
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandTimeout = DefaultCommandTimeoutSeconds;
                cmd.Parameters.Add(new SqlParameter("@q", SqlDbType.VarChar, 20) { Value = $"%{Truncate(q, 20)}%" });
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var mc = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                    var factory = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                    var process = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                    var dt = rdr.IsDBNull(3) ? string.Empty : rdr.GetDateTime(3).ToString("yyyy-MM-dd HH:mm:ss");
                    rows.Add((mc, factory, process, dt));
                }
            });
        });

        Console.Clear();
        TuiComponents.ShowPageHeader("MACHINE SEARCH - Results", "Machine Data > Machine Search", $"Query: {q}");
        if (rows.Count == 0)
        {
            TuiComponents.ShowError("No results.");
            TuiComponents.Pause();
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("mcName"); table.AddColumn("Factory"); table.AddColumn("Process"); table.AddColumn("DT_Update");
        foreach (var r in rows) table.AddRow(r.mcName, r.Factory, r.Process, r.DT_Update);
        AnsiConsole.Write(table);
        TuiComponents.Pause();
    }

    // Machine Lists: paged listing ordered by DT_Update DESC, mcName ASC
    private static async Task MachineListsAsync()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader("MACHINE LISTS", "Machine Data > Machine Lists", "Paged list of machines from Master_mcName");

        // select source
        if (!TrySelectSource(out var connectionString, out var dbName)) return;

        var targetTable = string.IsNullOrEmpty(dbName) ? "[dbo].[Master_mcName]" : $"[{dbName}].[dbo].[Master_mcName]";

        int pageSize = DefaultPageSize;
        var pageSizeInput = AnsiConsole.Ask<string>($"Page size (default {DefaultPageSize}):").Trim();
        if (!string.IsNullOrEmpty(pageSizeInput) && (!int.TryParse(pageSizeInput, out pageSize) || pageSize < 1 || pageSize > MaxPageSize))
        {
            TuiComponents.ShowError("Invalid page size.");
            TuiComponents.Pause();
            return;
        }

        // Keyset paging state: use (DT_Update DESC, mcName ASC). Keep lastDT and lastMcName.
        DateTime pageUpperDt = DateTime.MaxValue;
        string pageUpperMcName = string.Empty; // for first page, empty means start from top
        bool firstPage = true;

        bool exit = false;

        while (!exit)
        {
            var sql = $@"
SELECT TOP (@take) [mcName], [Factory], [Process], [DT_Update]
FROM {targetTable}
WHERE ( [DT_Update] < @upperDt OR ( [DT_Update] = @upperDt AND [mcName] > @upperMcName ) )
ORDER BY [DT_Update] DESC, [mcName] ASC;";

            var sb = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = Math.Clamp(DefaultConnectTimeoutSeconds, 5, 300) };
            var connStr = sb.ConnectionString;

            if (!TryTcpProbe(connStr, 5, out var probeError))
            {
                TuiComponents.ShowError(probeError ?? "TCP probe failed.");
                TuiComponents.Pause();
                return;
            }

            var rows = new List<(string mcName, string Factory, string Process, string DT_Update, DateTime DT)>();

            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Loading machines...", async _ =>
            {
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connStr);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.CommandTimeout = DefaultCommandTimeoutSeconds;
                    cmd.Parameters.Add(new SqlParameter("@take", SqlDbType.Int) { Value = pageSize + 1 });
                    cmd.Parameters.Add(new SqlParameter("@upperDt", SqlDbType.DateTime2) { Value = pageUpperDt });
                    cmd.Parameters.Add(new SqlParameter("@upperMcName", SqlDbType.VarChar, 20) { Value = Truncate(pageUpperMcName, 20) });
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var mc = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                        var factory = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                        var process = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                        var dtVal = rdr.IsDBNull(3) ? DateTime.MinValue : rdr.GetDateTime(3);
                        var dtStr = dtVal == DateTime.MinValue ? string.Empty : dtVal.ToString("yyyy-MM-dd HH:mm:ss");
                        rows.Add((mc, factory, process, dtStr, dtVal));
                    }
                });
            });

            bool hasMore = false;
            if (rows.Count > pageSize)
            {
                hasMore = true;
                rows.RemoveAt(rows.Count - 1);
            }

            Console.Clear();
            TuiComponents.ShowPageHeader("MACHINE LISTS - Results", "Machine Data > Machine Lists", $"Page {(firstPage ? 1 : 2)}");
            if (rows.Count == 0)
            {
                TuiComponents.ShowError("No data.");
            }
            else
            {
                var table = new Table().Border(TableBorder.Rounded).Expand();
                table.AddColumn("Index"); table.AddColumn("mcName"); table.AddColumn("Factory"); table.AddColumn("Process"); table.AddColumn("DT_Update");
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    table.AddRow(((i + 1).ToString()), r.mcName, r.Factory, r.Process, r.DT_Update);
                }
                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine($"[grey]Showing {rows.Count} rows{(hasMore ? " ... more available" : "")}[/]");
            }

            var choices = new List<string>();
            if (hasMore) choices.Add("Next Page");
            choices.Add("Search this page");
            choices.Add("Back");
            var pick = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Choose action:").AddChoices(choices));

            if (pick == "Next Page")
            {
                // advance: use last row's DT and mcName as the next upper bound
                var last = rows.LastOrDefault();
                if (last.DT != DateTime.MinValue)
                {
                    pageUpperDt = last.DT;
                    pageUpperMcName = last.mcName;
                    firstPage = false;
                }
                else
                {
                    TuiComponents.ShowError("No further pages.");
                    TuiComponents.Pause();
                }
            }
            else if (pick == "Search this page")
            {
                var term = AnsiConsole.Ask<string>("Enter mcName partial to search in this page:").Trim();
                var found = rows.Where(r => r.mcName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
                if (found.Count == 0)
                {
                    TuiComponents.ShowError("No matches on this page.");
                    TuiComponents.Pause();
                }
                else
                {
                    var table = new Table().Border(TableBorder.Rounded).Expand();
                    table.AddColumn("mcName"); table.AddColumn("Factory"); table.AddColumn("Process"); table.AddColumn("DT_Update");
                    foreach (var f in found) table.AddRow(f.mcName, f.Factory, f.Process, f.DT_Update);
                    AnsiConsole.Clear();
                    TuiComponents.ShowPageHeader("MACHINE LISTS - Search Results", "Machine Data > Machine Lists", $"Matches for '{term}'");
                    AnsiConsole.Write(table);
                    TuiComponents.Pause();
                }
            }
            else // Back
            {
                return;
            }
        }
    }

    // Helper: pick source and build connection string. Returns false if user cancelled.
    private static bool TrySelectSource(out string connectionString, out string? databaseName)
    {
        connectionString = string.Empty;
        databaseName = null;

        var storeMain = new ServerConnectionSettingsStore();
        var mainSettings = storeMain.Load(out _);
        if (!mainSettings.IsConfigured)
        {
            TuiComponents.ShowError("Main DB connection not configured.");
            TuiComponents.Pause();
            return false;
        }

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

        // Build connection string
        try
        {
            connectionString = BuildConnectionStringForSource(mainSettings, selectedHost, selectedPort, out var usedSettings);
            databaseName = usedSettings.Database;
            return true;
        }
        catch (Exception ex)
        {
            TuiComponents.ShowError($"Could not build connection string: {ex.Message}");
            TuiComponents.Pause();
            return false;
        }
    }

    // Build connection string (same logic as ProductControlUI)
    private static string BuildConnectionStringForSource(ServerConnectionSettings mainSettings, string selectedHost, int selectedPort, out ServerConnectionSettings usedSettings)
    {
        if (string.IsNullOrEmpty(selectedHost))
        {
            usedSettings = mainSettings;
            var connService = DatabaseConnectionServiceFactory.Create(mainSettings.Provider);
            if (connService is not SqlServerConnectionService) throw new InvalidOperationException("Expected SQL Server provider.");
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
            if (connService is not SqlServerConnectionService) throw new InvalidOperationException("Expected SQL Server provider.");
            var cs = ((SqlServerConnectionService)connService).BuildConnectionString(temp);
            var builder = new SqlConnectionStringBuilder(cs) { ConnectTimeout = Math.Clamp(temp.ConnectionTimeoutSeconds, 5, 300) };
            return builder.ConnectionString;
        }
    }

    // TCP probe
    private static bool TryTcpProbe(string connectionString, int probeTimeoutSeconds, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            var sb = new SqlConnectionStringBuilder(connectionString);
            var dataSource = (sb.DataSource ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(dataSource)) { errorMessage = "Empty data source"; return false; }
            if (dataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) dataSource = dataSource.Substring(4).Trim();
            if (dataSource.Contains("\\"))
            {
                var hostPart = dataSource.Split('\\', 2)[0].Trim();
                try { var addrs = System.Net.Dns.GetHostAddresses(hostPart); if (addrs == null || addrs.Length == 0) { errorMessage = $"Cannot resolve {hostPart}"; return false; } }
                catch (Exception ex) { errorMessage = $"DNS resolve failed: {ex.Message}"; return false; }
                return true;
            }
            string host = dataSource; int port = 1433;
            if (dataSource.Contains(",")) { var parts = dataSource.Split(',', 2); host = parts[0].Trim(); if (!int.TryParse(parts[1].Trim(), out port)) port = 1433; }
            if (string.IsNullOrEmpty(host)) { errorMessage = "Invalid host"; return false; }
            using var tcp = new TcpClient();
            var task = tcp.ConnectAsync(host, port);
            var ok = task.Wait(TimeSpan.FromSeconds(Math.Max(1, probeTimeoutSeconds)));
            if (!ok || !tcp.Connected) { errorMessage = $"Cannot reach {host}:{port}. TCP connection failed or timed out."; return false; }
            return true;
        }
        catch (Exception ex) { errorMessage = $"TCP probe failed: {ex.Message}"; return false; }
    }

    private static string Truncate(string? s, int maxLen) { if (string.IsNullOrEmpty(s)) return string.Empty; return s.Length <= maxLen ? s : s.Substring(0, maxLen); }
}