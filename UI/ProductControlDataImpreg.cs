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
    // PromptForFilters used by Data Impreg
    // --------------------
    private static (string mcName, string model, string serial, string rep, DateTime? dtFrom, DateTime? dtTo)
        PromptForFilters(string currentMc, string currentModel, string currentSerial, string currentRep, DateTime? currentFrom, DateTime? currentTo)
    {
        AnsiConsole.MarkupLine("[grey]Enter filter values (press ENTER to keep current, or type '-' to clear the filter).[/]");

        string ReadInput(string prompt, string current)
        {
            var raw = AnsiConsole.Ask<string>($"{prompt} (current: [yellow]{(string.IsNullOrEmpty(current) ? "-" : current)}[/]):").Trim();
            if (string.IsNullOrEmpty(raw))
                return current ?? string.Empty;
            if (raw == "-")
                return string.Empty;
            return raw;
        }

        var mcName = ReadInput("mcName contains", currentMc);
        var model = ReadInput("Model contains", currentModel);
        var serial = ReadInput("SerialNo contains", currentSerial);
        var rep = ReadInput("RepID equals (leave empty to ignore)", currentRep);

        DateTime? dtFrom = currentFrom;
        DateTime? dtTo = currentTo;

        string ReadDateInput(string prompt, DateTime? current)
        {
            var curStr = current.HasValue ? current.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            var raw = AnsiConsole.Ask<string>($"{prompt} (current: [yellow]{curStr}[/]) (ENTER keep, '-' clear):").Trim();
            return raw;
        }

        var dtFromRaw = ReadDateInput("DT from (e.g. 2026-08-18 22:27:50)", currentFrom);
        if (!string.IsNullOrEmpty(dtFromRaw))
        {
            if (dtFromRaw == "-")
                dtFrom = null;
            else if (DateTime.TryParse(dtFromRaw, out var df))
                dtFrom = df;
            else
            {
                TuiComponents.ShowError("DT from could not be parsed. Use a valid datetime format.");
                TuiComponents.Pause();
                return (currentMc, currentModel, currentSerial, currentRep, currentFrom, currentTo);
            }
        }

        var dtToRaw = ReadDateInput("DT to (e.g. 2026-08-18 22:27:50)", currentTo);
        if (!string.IsNullOrEmpty(dtToRaw))
        {
            if (dtToRaw == "-")
                dtTo = null;
            else if (DateTime.TryParse(dtToRaw, out var dt))
                dtTo = dt;
            else
            {
                TuiComponents.ShowError("DT to could not be parsed. Use a valid datetime format.");
                TuiComponents.Pause();
                return (currentMc, currentModel, currentSerial, currentRep, currentFrom, currentTo);
            }
        }

        return (mcName ?? string.Empty, model ?? string.Empty, serial ?? string.Empty, rep ?? string.Empty, dtFrom, dtTo);
    }
    // --------------------
    // Quick search by SerialNo (used in Data Impreg)
    // --------------------
    private static async Task<List<(string DT, string mcName, string Model, string SerialNo, int RepID)>> QuickSearchBySerial(string connectionString, string targetTable, string snInput, int commandTimeoutSeconds = DefaultCommandTimeoutSeconds, int top = 500)
    {
        var results = new List<(string, string, string, string, int)>();
        var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = Math.Clamp(commandTimeoutSeconds / 2, 5, 300) };
        var connStr = builder.ConnectionString;

        if (!TryTcpProbe(connStr, 5, out var probeError))
        {
            TuiComponents.ShowError(probeError ?? "TCP probe failed.");
            return results;
        }

        await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Searching SerialNo...", async _ =>
        {
            await Task.Run(() =>
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT TOP ({top}) [DT], [mcName], [Model], [SerialNo], [RepID] FROM {targetTable} WHERE [SerialNo] LIKE @sn ORDER BY [DT] DESC, [RepID] DESC;";
                cmd.CommandTimeout = commandTimeoutSeconds;
                cmd.Parameters.Add(new SqlParameter("@sn", SqlDbType.VarChar, 20) { Value = $"%{Truncate(snInput, 20)}%" });
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var dtVal = reader.IsDBNull(0) ? DateTime.MinValue : reader.GetDateTime(0);
                    var dtStr = reader.IsDBNull(0) ? string.Empty : dtVal.ToString("yyyy-MM-dd HH:mm:ss");
                    var mc = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var model = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    var serial = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    var repId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                    results.Add((dtStr, mc, model, serial, repId));
                }
            });
        });
        return results;
    }

    // --------------------
    // Data Impreg (keyset paging)
    // --------------------
    private static async Task ShowDataImpregInteractive()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader("DATA IMPREG", "Product Control Management > Data Impreg", "[B] Back");

        var storeMain = new ServerConnectionSettingsStore();
        var mainSettings = storeMain.Load(out _);
        if (!mainSettings.IsConfigured) { TuiComponents.ShowError("Database connection not configured."); TuiComponents.Pause(); return; }
        if (mainSettings.Provider != DatabaseProvider.SqlServer) { TuiComponents.ShowError("Requires SQL Server."); TuiComponents.Pause(); return; }

        // choose source
        if (!TrySelectSourceInternal(mainSettings, out var connectionString, out var usedSettings)) return;
        string targetTable = string.IsNullOrEmpty(usedSettings.Database) ? "[Minibae_Raw_Data].[dbo].[Impreg]" : $"[{usedSettings.Database}].[dbo].[Impreg]";

        // initial filters
        string mcNameFilter = string.Empty, modelFilter = string.Empty, serialFilter = string.Empty, repFilter = string.Empty;
        DateTime? dtFrom = null, dtTo = null;

        int pageSize = DefaultPageSize;
        var pageSizeInput = AnsiConsole.Ask<string>($"Page size (rows per page, default {DefaultPageSize}, max {MaxPageSize}):").Trim();
        if (!string.IsNullOrEmpty(pageSizeInput) && (!int.TryParse(pageSizeInput, out pageSize) || pageSize < 1 || pageSize > MaxPageSize))
        {
            TuiComponents.ShowError("Invalid page size."); TuiComponents.Pause(); return;
        }

        DateTime pageUpperDt = dtTo ?? DateTime.MaxValue;
        int pageUpperRep = int.MaxValue;

        while (true)
        {
            // build WHERE
            string baseWhere = "WHERE 1=1";
            var parameters = new List<SqlParameter>();
            if (!string.IsNullOrWhiteSpace(mcNameFilter)) { baseWhere += " AND [mcName] LIKE @mcName"; parameters.Add(new SqlParameter("@mcName", SqlDbType.VarChar, 20) { Value = $"%{Truncate(mcNameFilter, 20)}%" }); }
            if (!string.IsNullOrWhiteSpace(modelFilter)) { baseWhere += " AND [Model] LIKE @model"; parameters.Add(new SqlParameter("@model", SqlDbType.VarChar, 20) { Value = $"%{Truncate(modelFilter, 20)}%" }); }
            if (!string.IsNullOrWhiteSpace(serialFilter)) { baseWhere += " AND [SerialNo] LIKE @serial"; parameters.Add(new SqlParameter("@serial", SqlDbType.VarChar, 20) { Value = $"%{Truncate(serialFilter, 20)}%" }); }
            if (!string.IsNullOrWhiteSpace(repFilter) && int.TryParse(repFilter, out var repVal)) { baseWhere += " AND [RepID] = @rep"; parameters.Add(new SqlParameter("@rep", SqlDbType.Int) { Value = repVal }); }
            if (dtFrom.HasValue) { baseWhere += " AND [DT] >= @dtFrom"; parameters.Add(new SqlParameter("@dtFrom", SqlDbType.DateTime2) { Value = dtFrom.Value }); }

            string keysetWhere = baseWhere + " AND ( [DT] < @upperDt OR ( [DT] = @upperDt AND [RepID] < @upperRep ) )";
            var cmdParams = new List<SqlParameter>(parameters)
            {
                new SqlParameter("@upperDt", SqlDbType.DateTime2) { Value = pageUpperDt },
                new SqlParameter("@upperRep", SqlDbType.Int) { Value = pageUpperRep },
                new SqlParameter("@take", SqlDbType.Int) { Value = pageSize + 1 }
            };

            string pagedSql = $@"
SELECT TOP (@take) [DT], [mcName], [Model], [SerialNo], [RepID]
FROM {targetTable}
{keysetWhere}
ORDER BY [DT] DESC, [RepID] DESC;";

            var rows = new List<(DateTime DT, string DTs, string mcName, string Model, string SerialNo, int RepID)>();
            bool hasMore = false;

            try
            {
                var sb = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = Math.Clamp(DefaultConnectTimeoutSeconds, 5, 300) };
                var connStr = sb.ConnectionString;
                if (!TryTcpProbe(connStr, 5, out var probeError)) { TuiComponents.ShowError(probeError ?? "TCP probe failed."); TuiComponents.Pause(); return; }

                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Querying Data Impreg...", async _ =>
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
                            var dtVal = rdr.IsDBNull(0) ? DateTime.MinValue : rdr.GetDateTime(0);
                            var dtStr = dtVal == DateTime.MinValue ? string.Empty : dtVal.ToString("yyyy-MM-dd HH:mm:ss");
                            var mc = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                            var model = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                            var serial = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3);
                            var repId = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4);
                            rows.Add((dtVal, dtStr, mc, model, serial, repId));
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
            if (rows.Count > 0) { pageUpperDt = rows.Last().DT; pageUpperRep = rows.Last().RepID; }

            Console.Clear();
            TuiComponents.ShowPageHeader("DATA IMPREG - Results", "Product Control Management > Data Impreg", $"Showing {rows.Count} rows{(hasMore ? " ...more" : "")}");
            if (rows.Count == 0) { TuiComponents.ShowError("No data."); TuiComponents.Pause(); return; }

            var table = new Table().Border(TableBorder.Rounded).Expand();
            table.AddColumn("Index"); table.AddColumn("DT"); table.AddColumn("mcName"); table.AddColumn("Model"); table.AddColumn("SerialNo"); table.AddColumn("RepID");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                table.AddRow((i + 1).ToString(), r.DTs, r.mcName, r.Model, r.SerialNo, r.RepID.ToString());
            }
            AnsiConsole.Write(table);

            var navChoices = new List<string>();
            if (hasMore) navChoices.Add("Next Page");
            navChoices.Add("View Row Detail");
            navChoices.Add("Change Filters");
            navChoices.Add("Back");

            var nav = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Choose action:").AddChoices(navChoices));

            if (nav == "Next Page")
            {
                // already updated pageUpperDt/pageUpperRep above
                continue;
            }
            else if (nav == "View Row Detail")
            {
                var idx = AnsiConsole.Prompt(new TextPrompt<int>("Enter row index:").Validate(i => i >= 1 && i <= rows.Count ? ValidationResult.Success() : ValidationResult.Error("[red]Index out of range[/]")));
                var r = rows[idx - 1];
                var panel = new Panel($"[yellow]DT:[/] {r.DTs}\n[yellow]mcName:[/] {r.mcName}\n[yellow]Model:[/] {r.Model}\n[yellow]SerialNo:[/] {r.SerialNo}\n[yellow]RepID:[/] {r.RepID}")
                    .Header("Data Impreg - Row Detail", Justify.Center)
                    .Expand();
                AnsiConsole.Write(panel);
                TuiComponents.Pause();
            }
            else if (nav == "Change Filters")
            {
                var pf = PromptForFilters(mcNameFilter, modelFilter, serialFilter, repFilter, dtFrom, dtTo);
                mcNameFilter = pf.mcName;
                modelFilter = pf.model;
                serialFilter = pf.serial;
                repFilter = pf.rep;
                dtFrom = pf.dtFrom;
                dtTo = pf.dtTo;
                // reset paging
                pageUpperDt = dtTo ?? DateTime.MaxValue;
                pageUpperRep = int.MaxValue;
                continue;
            }
            else // Back
            {
                return;
            }
        }
    }

}
