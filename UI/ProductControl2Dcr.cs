using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spectre.Console;
using CompanyCLI.Configuration;
using CompanyCLI.Services;
using Microsoft.Data.SqlClient;

namespace CompanyCLI.UI;

public static partial class ProductControlUI
{
    // --------------------
    // 2DCR - RESULT viewer
    // --------------------
    private static async Task Show2DcrResultsAsync()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader("2DCR - RESULT", "Product Control Management > 2DCR - RESULT", "Show Status, Name, Detail from 2DCR_Result");

        var storeMain = new ServerConnectionSettingsStore();
        var mainSettings = storeMain.Load(out _);
        if (!mainSettings.IsConfigured) { TuiComponents.ShowError("DB not configured."); TuiComponents.Pause(); return; }
        if (mainSettings.Provider != DatabaseProvider.SqlServer) { TuiComponents.ShowError("Requires SQL Server."); TuiComponents.Pause(); return; }

        if (!TrySelectSourceInternal(mainSettings, out var connectionString, out var usedSettings)) return;
        string targetTable = string.IsNullOrEmpty(usedSettings.Database) ? "[Minibae_Raw_Data].[dbo].[2DCR_Result]" : $"[{usedSettings.Database}].[dbo].[2DCR_Result]";

        var sql = $@"
SELECT TOP (500) [Status], [Name], [Detail]
FROM {targetTable}
ORDER BY [Name] ASC;";

        var sb = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = Math.Clamp(DefaultConnectTimeoutSeconds, 5, 300) };
        var connStr = sb.ConnectionString;
        if (!TryTcpProbe(connStr, 5, out var probeError)) { TuiComponents.ShowError(probeError ?? "TCP probe failed."); TuiComponents.Pause(); return; }

        var rows = new List<(string Status, string Name, string Detail)>();
        try
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Loading 2DCR results...", async _ =>
            {
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connStr);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.CommandTimeout = DefaultCommandTimeoutSeconds;
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var status = rdr.IsDBNull(0) ? string.Empty : rdr.GetValue(0)?.ToString() ?? string.Empty;
                        var name = rdr.IsDBNull(1) ? string.Empty : rdr.GetValue(1)?.ToString() ?? string.Empty;
                        var detail = rdr.IsDBNull(2) ? string.Empty : rdr.GetValue(2)?.ToString() ?? string.Empty;
                        rows.Add((status, name, detail));
                    }
                });
            });
        }
        catch (SqlException sex) when (sex.Number == -2 || sex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            TuiComponents.ShowError("Query timed out. Try again or use a different source.");
            TuiComponents.Pause();
            return;
        }
        catch (Exception ex) { TuiComponents.ShowError($"Failed to load 2DCR results: {ex.Message}"); TuiComponents.Pause(); return; }

        Console.Clear();
        TuiComponents.ShowPageHeader("2DCR - RESULT", "Product Control Management > 2DCR - RESULT", $"Rows: {rows.Count}");
        if (rows.Count == 0) { TuiComponents.ShowError("No data found."); TuiComponents.Pause(); return; }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Status"); table.AddColumn("Name"); table.AddColumn("Detail");
        foreach (var r in rows) table.AddRow(r.Status, r.Name, r.Detail);
        AnsiConsole.Write(table);
        TuiComponents.Pause();
    }
}
