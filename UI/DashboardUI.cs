using CompanyCLI.Commands;
using Spectre.Console;

namespace CompanyCLI.UI;

public static class DashboardUI
{
    public static async Task ShowAsync()
    {
        await SettingsCommand.TestConnectionAutomaticallyAsync();

        while (true)
        {
            Console.Clear();

            DrawHeader();

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]Navigation[/]")
                    .PageSize(10)
                    .HighlightStyle(
                        new Style(
                            foreground: Color.Cyan1,
                            decoration: Decoration.Bold
                        )
                    )
                    .AddChoices(
                        "Dashboard",
                        "Employee Management",
                        "Project Management",
                        "Task Management",
                        "Reports",
                        "Settings",
                        "Logout"
                    )
            );

            switch (selected)
            {
                case "Dashboard":
                    if (DrawDashboard())
                    {
                        return;
                    }
                    break;

                case "Employee Management":
                    EmployeeCommand.Run();
                    break;

                case "Project Management":
                    ShowComingSoon("Project Management");
                    break;

                case "Task Management":
                    ShowComingSoon("Task Management");
                    break;

                case "Reports":
                    ShowComingSoon("Reports");
                    break;

                case "Settings":
                    await SettingsCommand.RunAsync();
                    break;

                case "Logout":
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
        var header = new Panel(
            Align.Center(
                new Markup(
                    "[bold cyan]PRODUCT MANAGEMENT SYSTEM[/]\n" +
                    "[grey]DIACAST DIVISION BPI[/]"
                ),
                VerticalAlignment.Middle
            )
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };

        AnsiConsole.Write(header);

        AnsiConsole.WriteLine();

        var userInfo = new Table
        {
            Border = TableBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Title = new TableTitle("[bold cyan]Session Information[/]"),
            Expand = true
        };

        userInfo
            .AddColumn(new TableColumn("[grey]USER[/]").Centered())
            .AddColumn(new TableColumn("[grey]SYSTEM[/]").Centered())
            .AddColumn(new TableColumn("[grey]VERSION[/]").Centered())
            .AddColumn(new TableColumn("[grey]DATABASE[/]").Centered());

        userInfo.AddRow(
            "[cyan]Administrator[/]",
            "[green]● Online[/]",
            "[yellow]1.0.0[/]",
            SettingsCommand.DatabaseConnectionStatus
        );

        AnsiConsole.Write(userInfo);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[grey]Dashboard[/]").RuleStyle("grey").LeftJustified());
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
                    $"[bold cyan]{EmployeeCommand.Count}[/]\n" +
                    $"[green]● {EmployeeCommand.ActiveCount} Active[/]"
                ),
                VerticalAlignment.Middle
            )
        )
        {
            Header = new PanelHeader("[bold]EMPLOYEES[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
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
            Title = new TableTitle("[bold cyan]Recent Activities[/]"),
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

        AnsiConsole.MarkupLine("[grey]Type [cyan]help[/] to see available commands.[/]");
        var command = AnsiConsole.Ask<string>(
            "[bold cyan]company>[/]"
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

            case "project":
            case "projects":
                ShowComingSoon("Project Management");
                return false;

            case "task":
            case "tasks":
                ShowComingSoon("Task Management");
                return false;

            case "report":
            case "reports":
                ShowComingSoon("Reports");
                return false;

            case "dashboard":
            case "home":
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
            BorderStyle = new Style(Color.Cyan1),
            Title = new TableTitle("[bold cyan]Commands[/]"),
            Expand = true
        };

        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow(
            "employees",
            "Open employee management"
        );

        table.AddRow(
            "projects",
            "Open project management"
        );

        table.AddRow(
            "tasks",
            "Open task management"
        );

        table.AddRow(
            "reports",
            "Open reports"
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

    private static void ShowComingSoon(string module)
    {
        Console.Clear();

        TuiComponents.ShowPageHeader(
            module.ToUpperInvariant(),
            $"Dashboard > {module}",
            "[B] Back to dashboard"
        );

        var panel = new Panel(
            Align.Center(
                new Markup(
                    $"[bold cyan]{module}[/]\n\n" +
                    "[yellow]Module is under development.[/]"
                ),
                VerticalAlignment.Middle
            )
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(3, 2),
            Expand = true
        };

        AnsiConsole.Write(panel);
        TuiComponents.Pause("Press ENTER to return to dashboard...");
    }
}