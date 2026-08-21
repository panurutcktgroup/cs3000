using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using CompanyCLI.Configuration;
using CompanyCLI.Services;
using Microsoft.Data.SqlClient;

namespace CompanyCLI.UI;

public static partial class ProductControlUI
{
    // --------------------
    // WOS-TAG (uses keyset paging and WOS search)
    // --------------------
    private record WosFilters(string Process, string SerialNo, DateTime? DtFrom, DateTime? DtTo, string WOS, string Machine, string Jdge, string Model);

    private static WosFilters PromptWosFilters()
    {
        AnsiConsole.MarkupLine("[grey]Enter filter values (ENTER keep blank).[/]");
        string Read(string prompt) => AnsiConsole.Ask<string>($"{prompt}:").Trim();

        var process = Read("Process");
        var serial = Read("SerialNo (partial)");
        var wos = Read("WOS (exact)");
        var machine = Read("Machine");
        var jdg = Read("Jdge");
        var model = Read("Model");
        DateTime? dtFrom = null, dtTo = null;
        var dtFromStr = Read("DT From (yyyy-MM-dd HH:mm:ss)");
        if (!string.IsNullOrEmpty(dtFromStr) && DateTime.TryParse(dtFromStr, out var df)) dtFrom = df;
        var dtToStr = Read("DT To (yyyy-MM-dd HH:mm:ss)");
        if (!string.IsNullOrEmpty(dtToStr) && DateTime.TryParse(dtToStr, out var dt)) dtTo = dt;

        return new WosFilters(process, serial, dtFrom, dtTo, wos, machine, jdg, model);
    }

    private static async Task ShowWosTagAsync()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader("WOS-TAG", "Product Control Management > WOS-TAG", "List / Filter / Search WOS_Tag");

        var storeMain = new ServerConnectionSettingsStore();
        var mainSettings = storeMain.Load(out _);
        if (!mainSettings.IsConfigured) { TuiComponents.ShowError("Database connection not configured."); TuiComponents.Pause(); return; }
        if (mainSettings.Provider != DatabaseProvider.SqlServer) { TuiComponents.ShowError("WOS-Tag requires SQL Server."); TuiComponents.Pause(); return; }

        if (!TrySelectSourceInternal(mainSettings, out var connectionString, out var usedSettings)) return;
        var targetTable = string.IsNullOrEmpty(usedSettings.Database) ? "[Minibae_Raw_Data].[dbo].[WOS_Tag]" : $"[{usedSettings.Database}].[dbo].[WOS_Tag]";

        while (true)
        {
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("WOS-TAG - Choose action")
                .AddChoices(new[] { "List Recent (paged)", "Filter Results", "Search by WOS", "Back" }));

            if (choice == "Back") break;

            if (choice == "Search by WOS")
            {
                var wos = AnsiConsole.Ask<string>("Enter WOS to search (exact):").Trim();
                if (string.IsNullOrEmpty(wos)) { TuiComponents.ShowError("WOS required."); TuiComponents.Pause(); continue; }
                await ShowRowsByWos(connectionString, targetTable, wos);
            }
            else if (choice == "Filter Results")
            {
                var filters = PromptWosFilters();
                await ShowPagedWos(connectionString, targetTable, filters);
            }
            else if (choice == "List Recent (paged)")
            {
                var filters = new WosFilters(string.Empty, string.Empty, null, null, string.Empty, string.Empty, string.Empty, string.Empty);
                await ShowPagedWos(connectionString, targetTable, filters);
            }
        }
    }

    private static async Task ShowRowsByWos(string connectionString, string targetTable, string wos)
    {
        const int maxRows = 2000;
        var sql = $@"
SELECT TOP (@top) [Process],[SerialNo],[DT],[WOS],[Machine],[Jdge],[Model]
FROM {targetTable}
WHERE [WOS] = @wos
ORDER BY [DT] DESC, [SerialNo] DESC;";

        var sb = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = Math.Clamp(DefaultConnectTimeoutSeconds, 5, 300) };
        var connStr = sb.ConnectionString;
        if (!TryTcpProbe(connStr, 5, out var probeError)) { TuiComponents.ShowError(probeError ?? "TCP probe failed."); TuiComponents.Pause(); return; }

        var rows = new List<(string Process, string SerialNo, string DT, string WOS, string Machine, string Jdge, string Model)>();
        try
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Loading WOS rows...", async _ =>
            {
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connStr);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.CommandTimeout = DefaultCommandTimeoutSeconds;
                    cmd.Parameters.Add(new SqlParameter("@top", SqlDbType.Int) { Value = maxRows });
                    cmd.Parameters.Add(new SqlParameter("@wos", SqlDbType.VarChar, 100) { Value = Truncate(wos, 100) });
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var process = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                        var serial = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                        var dtVal = rdr.IsDBNull(2) ? DateTime.MinValue : rdr.GetDateTime(2);
                        var dtStr = dtVal == DateTime.MinValue ? string.Empty : dtVal.ToString("yyyy-MM-dd HH:mm:ss");
                        var w = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3);
                        var machine = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4);
                        var jdg = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5);
                        var model = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6);
                        rows.Add((process, serial, dtStr, w, machine, jdg, model));
                    }
                });
            });
        }
        catch (Exception ex) { TuiComponents.ShowError($"Failed to load WOS rows: {ex.Message}"); TuiComponents.Pause(); return; }

        Console.Clear();
        TuiComponents.ShowPageHeader("WOS-TAG - Results", $"WOS = {wos}", $"Rows: {rows.Count}");
        if (rows.Count == 0) { TuiComponents.ShowError("No data found."); TuiComponents.Pause(); return; }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Process"); table.AddColumn("SerialNo"); table.AddColumn("DT"); table.AddColumn("WOS"); table.AddColumn("Machine"); table.AddColumn("Jdge"); table.AddColumn("Model");
        foreach (var r in rows) table.AddRow(r.Process, r.SerialNo, r.DT, r.WOS, r.Machine, r.Jdge, r.Model);
        AnsiConsole.Write(table);

        if (AnsiConsole.Confirm("View detail of a SerialNo from the list?", false))
        {
            var serial = AnsiConsole.Ask<string>("Enter SerialNo to view:");
            var found = rows.FirstOrDefault(x => string.Equals(x.SerialNo, serial, StringComparison.OrdinalIgnoreCase));
            if (found.SerialNo == null) { TuiComponents.ShowError("SerialNo not found in loaded results."); TuiComponents.Pause(); }
            else
            {
                var panel = new Panel($"[yellow]Process:[/] {found.Process}\n[yellow]SerialNo:[/] {found.SerialNo}\n[yellow]DT:[/] {found.DT}\n[yellow]WOS:[/] {found.WOS}\n[yellow]Machine:[/] {found.Machine}\n[yellow]Jdge:[/] {found.Jdge}\n[yellow]Model:[/] {found.Model}")
                    .Header("WOS Row Detail", Justify.Center)
                    .Expand();
                AnsiConsole.Write(panel);
                TuiComponents.Pause();
            }
        }
        else
        {
            TuiComponents.Pause();
        }
    }

    private static async Task ShowPagedWos(string connectionString, string targetTable, WosFilters filters)
    {
        int pageSize = DefaultPageSize;
        var pageSizeInput = AnsiConsole.Ask<string>($"Page size (rows per page, default {DefaultPageSize}, max {MaxPageSize}):").Trim();
        if (!string.IsNullOrEmpty(pageSizeInput))
        {
            if (!int.TryParse(pageSizeInput, out pageSize) || pageSize <= 0 || pageSize > MaxPageSize)
            {
                TuiComponents.ShowError($"Invalid page size. Enter 1..{MaxPageSize}.");
                TuiComponents.Pause();
                return;
            }
        }

        DateTime pageUpperDt = filters.DtTo ?? DateTime.MaxValue;
        string pageUpperSerial = string.Empty;

        while (true)
        {
            string baseWhere = "WHERE 1=1";
            var parameters = new List<SqlParameter>();
            if (!string.IsNullOrWhiteSpace(filters.Process)) { baseWhere += " AND [Process] LIKE @process"; parameters.Add(new SqlParameter("@process", SqlDbType.VarChar, 100) { Value = $"%{Truncate(filters.Process, 100)}%" }); }
            if (!string.IsNullOrWhiteSpace(filters.SerialNo)) { baseWhere += " AND [SerialNo] LIKE @serial"; parameters.Add(new SqlParameter("@serial", SqlDbType.VarChar, 100) { Value = $"%{Truncate(filters.SerialNo, 100)}%" }); }
            if (!string.IsNullOrWhiteSpace(filters.WOS)) { baseWhere += " AND [WOS] = @wos"; parameters.Add(new SqlParameter("@wos", SqlDbType.VarChar, 100) { Value = Truncate(filters.WOS, 100) }); }
            if (!string.IsNullOrWhiteSpace(filters.Machine)) { baseWhere += " AND [Machine] LIKE @machine"; parameters.Add(new SqlParameter("@machine", SqlDbType.VarChar, 100) { Value = $"%{Truncate(filters.Machine, 100)}%" }); }
            if (!string.IsNullOrWhiteSpace(filters.Jdge)) { baseWhere += " AND [Jdge] LIKE @jdg"; parameters.Add(new SqlParameter("@jdg", SqlDbType.VarChar, 100) { Value = $"%{Truncate(filters.Jdge, 100)}%" }); }
            if (!string.IsNullOrWhiteSpace(filters.Model)) { baseWhere += " AND [Model] LIKE @model"; parameters.Add(new SqlParameter("@model", SqlDbType.VarChar, 100) { Value = $"%{Truncate(filters.Model, 100)}%" }); }
            if (filters.DtFrom.HasValue) { baseWhere += " AND [DT] >= @dtFrom"; parameters.Add(new SqlParameter("@dtFrom", SqlDbType.DateTime2) { Value = filters.DtFrom.Value }); }

            string keysetWhere = baseWhere + " AND ( [DT] < @upperDt OR ( [DT] = @upperDt AND [SerialNo] < @upperSerial ) )";
            var keysetParams = new List<SqlParameter>(parameters)
            {
                new SqlParameter("@upperDt", SqlDbType.DateTime2) { Value = pageUpperDt },
                new SqlParameter("@upperSerial", SqlDbType.VarChar, 100) { Value = Truncate(pageUpperSerial, 100) }
            };
            var cmdParams = new List<SqlParameter>(keysetParams) { new SqlParameter("@take", SqlDbType.Int) { Value = pageSize + 1 } };

            string pagedSql = $@"
SELECT TOP (@take) [Process],[SerialNo],[DT],[WOS],[Machine],[Jdge],[Model]
FROM {targetTable}
{keysetWhere}
ORDER BY [DT] DESC, [SerialNo] DESC;";

            var rows = new List<(string Process, string SerialNo, DateTime DT, string DTs, string WOS, string Machine, string Jdge, string Model)>();
            bool hasMore = false;

            try
            {
                var sb = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = Math.Clamp(DefaultConnectTimeoutSeconds, 5, 300) };
                var connStr = sb.ConnectionString;
                if (!TryTcpProbe(connStr, 5, out var probeError)) { TuiComponents.ShowError(probeError ?? "TCP probe failed."); TuiComponents.Pause(); return; }

                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Querying WOS_Tag (paged)...", async _ =>
                {
                    await Task.Run(() =>
                    {
                        using var conn = new SqlConnection(connStr);
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = pagedSql;
                        cmd.CommandTimeout = DefaultCommandTimeoutSeconds;
                        foreach (var p in cmdParams) cmd.Parameters.Add(p);
                        using var rdr = cmd.ExecuteReader();
                        while (rdr.Read())
                        {
                            var process = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                            var serial = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                            var dtVal = rdr.IsDBNull(2) ? DateTime.MinValue : rdr.GetDateTime(2);
                            var dtStr = dtVal == DateTime.MinValue ? string.Empty : dtVal.ToString("yyyy-MM-dd HH:mm:ss");
                            var wos = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3);
                            var machine = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4);
                            var jdg = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5);
                            var model = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6);
                            rows.Add((process, serial, dtVal, dtStr, wos, machine, jdg, model));
                        }
                    });
                });
            }
            catch (SqlException sex) when (sex.Number == -2 || sex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                TuiComponents.ShowError("Query timed out. You can retry or change filters.");
                if (AnsiConsole.Confirm("Retry?", true)) continue;
                return;
            }
            catch (Exception ex) { TuiComponents.ShowError($"Query failed: {ex.Message}"); if (AnsiConsole.Confirm("Retry?", false)) continue; return; }

            if (rows.Count > pageSize) { hasMore = true; rows.RemoveAt(rows.Count - 1); }

            Console.Clear();
            TuiComponents.ShowPageHeader("WOS-TAG - Results", "Product Control Management > WOS-TAG", $"Showing {rows.Count} rows{(hasMore ? " ...more" : "")}");
            var table = new Table().Border(TableBorder.Rounded).Expand();
            table.AddColumn("Index"); table.AddColumn("Process"); table.AddColumn("SerialNo"); table.AddColumn("DT"); table.AddColumn("WOS"); table.AddColumn("Machine"); table.AddColumn("Jdge"); table.AddColumn("Model");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                table.AddRow((i + 1).ToString(), r.Process, r.SerialNo, r.DTs, r.WOS, r.Machine, r.Jdge, r.Model);
            }
            AnsiConsole.Write(table);

            var navChoices = new List<string>();
            if (hasMore) navChoices.Add("Next Page");
            navChoices.Add("View Row Detail");
            navChoices.Add("Search WOS (quick)");
            navChoices.Add("Change Filters");
            navChoices.Add("Back");

            var nav = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Choose action:").AddChoices(navChoices));

            if (nav == "Next Page")
            {
                var last = rows.Last();
                pageUpperDt = last.DT;
                pageUpperSerial = last.SerialNo;
                continue;
            }
            else if (nav == "View Row Detail")
            {
                var idx = AnsiConsole.Prompt(new TextPrompt<int>("Enter row index:").Validate(i => i >= 1 && i <= rows.Count ? ValidationResult.Success() : ValidationResult.Error("[red]Index out of range[/]")));
                var r = rows[idx - 1];
                var panel = new Panel($"[yellow]Process:[/] {r.Process}\n[yellow]SerialNo:[/] {r.SerialNo}\n[yellow]DT:[/] {r.DTs}\n[yellow]WOS:[/] {r.WOS}\n[yellow]Machine:[/] {r.Machine}\n[yellow]Jdge:[/] {r.Jdge}\n[yellow]Model:[/] {r.Model}")
                    .Header("WOS Row Detail", Justify.Center)
                    .Expand();
                AnsiConsole.Write(panel);
                TuiComponents.Pause();
            }
            else if (nav == "Search WOS (quick)")
            {
                var wos = AnsiConsole.Ask<string>("WOS to search (exact):").Trim();
                if (!string.IsNullOrEmpty(wos)) await ShowRowsByWos(connectionString, targetTable, wos);
            }
            else if (nav == "Change Filters")
            {
                filters = PromptWosFilters();
                pageUpperDt = filters.DtTo ?? DateTime.MaxValue;
                pageUpperSerial = string.Empty;
                continue;
            }
            else // Back
            {
                return;
            }
        }
    }
}
