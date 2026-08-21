using CompanyCLI.Commands;
using Spectre.Console;
using System.Text;

namespace CompanyCLI.UI;

public static class DashboardUI
{
    private static readonly Color PrimaryColor = Color.FromHex("#005aab");

    public static async Task ShowAsync()
    {
        while (true)
        {
            Console.Clear();

            DrawHeader();

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold rgb(0,90,171)]Navigation[/]")
                    .PageSize(6)
                    .HighlightStyle(
                        new Style(
                            foreground: PrimaryColor,
                            decoration: Decoration.Bold
                        )
                    )
                    .AddChoices(
                        "Product Control Management",
                        "Machine Data",
                        "Serial Number Traceability",
                        "Settings",
                        "Logout"
                    )
            );

            switch (selected)
            {
                case string value when value.StartsWith("Product Control Management", StringComparison.Ordinal):
                    await ProductControlUI.ShowAsync();
                    break;

                case string value when value.StartsWith("Machine Data", StringComparison.Ordinal):
                    await MachineDataUI.ShowAsync();
                    break;

                case string value when value.StartsWith("Serial Number Traceability", StringComparison.Ordinal):
                    await SerialNumberTraceabilityCommand.RunAsync();
                    break;

                case string value when value.StartsWith("Settings", StringComparison.Ordinal):
                    if (await SettingsAuthenticationCommand.RunAsync())
                    {
                        await SettingsCommand.RunAsync();
                    }
                    break;

                case string value when value.StartsWith("Logout", StringComparison.Ordinal):
                    if (AnsiConsole.Confirm("Logout from the application?", false))
                    {
                        return;
                    }
                    break;
            }
        }
    }

    private static void DrawHeader()
    {
        var pixelLogo = BuildPixelLogo("MINEBEAMITSUMI");
        var header = new Panel(
            Align.Center(
                new Markup(
                    $"[bold rgb(0,90,171)]{pixelLogo}[/]\n\n" +
                    "[italic rgb(237,28,36)]Passion[/] " +
                    "[italic rgb(0,90,171)]to Create Value through[/] " +
                    "[italic rgb(237,28,36)]Difference[/]\n\n" +
                    "[grey]PRODUCT MANAGEMENT SYSTEM[/]\n" +
                    "[grey]DIACAST DIVISION BPI[/]"
                ),
                VerticalAlignment.Middle
            )
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(PrimaryColor),
            Padding = new Padding(2, 1),
            Expand = true
        };

        AnsiConsole.Write(header);

        AnsiConsole.WriteLine();

        var userInfo = new Table
        {
            Border = TableBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Title = new TableTitle("[bold rgb(0,90,171)]Session Information[/]"),
            Expand = true
        };

        userInfo
            .AddColumn(new TableColumn("[grey]USER[/]").Centered())
            .AddColumn(new TableColumn("[grey]SYSTEM[/]").Centered())
            .AddColumn(new TableColumn("[grey]VERSION[/]").Centered())
            .AddColumn(new TableColumn("[grey]DATABASE[/]").Centered());

        userInfo.AddRow(
            "[rgb(0,90,171)]Administrator[/]",
            "[green]● Online[/]",
            "[yellow]1.0.0[/]",
            SettingsCommand.DatabaseConnectionStatus
        );

        AnsiConsole.Write(userInfo);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[grey]Dashboard[/]").RuleStyle("grey").LeftJustified());
    }

    private static string BuildPixelLogo(string text)
    {
        var pixels = new Dictionary<char, string[]>
        {
            ['M'] = new[] { "10001", "11011", "10101", "10001", "10001" },
            ['I'] = new[] { "11111", "00100", "00100", "00100", "11111" },
            ['N'] = new[] { "10001", "11001", "10101", "10011", "10001" },
            ['E'] = new[] { "11111", "10000", "11110", "10000", "11111" },
            ['B'] = new[] { "11110", "10001", "11110", "10001", "11110" },
            ['A'] = new[] { "01110", "10001", "11111", "10001", "10001" },
            ['T'] = new[] { "11111", "00100", "00100", "00100", "00100" },
            ['S'] = new[] { "01111", "10000", "01110", "00001", "11110" },
            ['U'] = new[] { "10001", "10001", "10001", "10001", "01110" }
        };

        var result = new StringBuilder();
        for (var row = 0; row < 5; row++)
        {
            foreach (var character in text)
            {
                if (!pixels.TryGetValue(character, out var pattern))
                    continue;

                foreach (var pixel in pattern[row])
                    result.Append(pixel == '1' ? '█' : ' ');

                result.Append("  ");
            }

            if (row < 4)
                result.AppendLine();
        }

        return result.ToString();
    }

    private static bool DrawDashboard()
    {
        Console.Clear();

        DrawHeader();
        AnsiConsole.MarkupLine("[grey]Overview of the current in-memory system data[/]");
        AnsiConsole.WriteLine();

        var employees = new Panel(
            Align.Center(
                new Markup(
                    $"[bold rgb(0,90,171)]{EmployeeCommand.Count}[/]\n" +
                    $"[green]● {EmployeeCommand.ActiveCount} Active[/]"
                ),
                VerticalAlignment.Middle
            )
        )
        {
            Header = new PanelHeader("[bold]EMPLOYEES[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(PrimaryColor),
            Padding = new Padding(2, 1),
            Expand = true
        };

        var projects = new Panel(
            Align.Center(
                new Markup(
                    "[bold yellow]24[/]\n" +
                    "[yellow]● Running[/]"
                ),
                VerticalAlignment.Middle
            )
        )
        {
            Header = new PanelHeader("[bold]PROJECTS[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1),
            Expand = true
        };

        var tasks = new Panel(
            Align.Center(
                new Markup(
                    "[bold green]138[/]\n" +
                    "[green]● Active[/]"
                ),
                VerticalAlignment.Middle
            )
        )
        {
            Header = new PanelHeader("[bold]TASKS[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1),
            Expand = true
        };

        var statistics = new Columns(
            employees,
            projects,
            tasks
        );

        AnsiConsole.Write(statistics);

        AnsiConsole.WriteLine();

        var activityTable = new Table()
        {
            Border = TableBorder.Rounded,
            Title = new TableTitle("[bold rgb(0,90,171)]Recent Activities[/]"),
            BorderStyle = new Style(Color.Grey)
        };

        activityTable.AddColumn("[bold]Time[/]");
        activityTable.AddColumn("[bold]Activity[/]");
        activityTable.AddColumn("[bold]User[/]");
        activityTable.AddColumn("[bold]Status[/]");

        activityTable.AddRow(
            "10:42",
            "Project #102 completed",
            "Admin",
            "[green]Completed[/]"
        );

        activityTable.AddRow(
            "10:18",
            "Task #204 assigned",
            "Somchai",
            "[yellow]Running[/]"
        );

        activityTable.AddRow(
            "09:55",
            "Employee #056 created",
            "Admin",
            "[green]Success[/]"
        );

        activityTable.AddRow(
            "09:31",
            "Database backup",
            "System",
            "[green]Success[/]"
        );

        activityTable.Columns[0].Width = 8;
        activityTable.Columns[2].Width = 14;
        activityTable.Columns[3].Width = 14;

        AnsiConsole.Write(activityTable);

        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[grey]Type [rgb(0,90,171)]help[/] to see available commands.[/]");
        var command = AnsiConsole.Ask<string>(
            "[bold rgb(0,90,171)]company>"
        );

        return HandleCommand(command);
    }

    private static bool HandleCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();

        switch (command)
        {
            case "employee":
            case "employees":
                EmployeeCommand.Run();
                return false;

            case "product":
            case "products":
            case "product control":
            case "product control management":
            case "product management":
                // Handle synchronous call to async ShowAsync()
                ProductControlUI.ShowAsync().GetAwaiter().GetResult();
                return false;

            case "machine":
            case "machines":
            case "machine data":
            case "machinedata":
                // allow command to open Machine Data UI
                MachineDataUI.ShowAsync().GetAwaiter().GetResult();
                return false;

            case "dashboard":
            case "home":
                // keep as no-op (stay on dashboard)
                return false;

            case "clear":
                return false;

            case "help":
                ShowHelp();
                return false;

            case "logout":
            case "exit":
                return AnsiConsole.Confirm("Logout from the application?", false);

            default:
                TuiComponents.ShowError("Unknown command. Type help for available commands.");
                TuiComponents.Pause();
                return false;
        }
    }

    private static void ShowHelp()
    {
        Console.Clear();

        TuiComponents.ShowPageHeader(
            "AVAILABLE COMMANDS",
            "Dashboard > Help",
            "Type a command at the company> prompt"
        );

        var table = new Table()
        {
            Border = TableBorder.Rounded,
            BorderStyle = new Style(PrimaryColor),
            Title = new TableTitle("[bold rgb(0,90,171)]Commands[/]"),
            Expand = true
        };

        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow(
            "employees",
            "Open employee management"
        );

        table.AddRow(
            "products",
            "Open product management"
        );

        table.AddRow(
            "machines",
            "Open machine data (search / lists)"
        );

        table.AddRow(
            "help",
            "Show available commands"
        );

        table.AddRow(
            "clear",
            "Clear terminal"
        );

        table.AddRow(
            "dashboard / home",
            "Return to the dashboard"
        );

        table.AddRow(
            "logout / exit",
            "Logout from the application"
        );

        AnsiConsole.Write(table);

        TuiComponents.Pause();
    }

}