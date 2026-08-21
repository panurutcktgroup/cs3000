using CompanyCLI.Commands;
using Spectre.Console;

namespace CompanyCLI.UI;

public static class StartupUI
{
    private const string ApplicationVersion = "1.0.0";

    public static async Task ShowAsync()
    {
        Console.Clear();

        AnsiConsole.Write(
            new FigletText("CS3000")
                .Centered()
                .Color(Color.Cyan1));

        var systemPanel = new Panel(
            Align.Center(
                new Markup("[bold cyan]PRODUCT MANAGEMENT SYSTEM[/]\n[grey]DIACAST DIVISION BPI[/]"),
                VerticalAlignment.Middle))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };

        AnsiConsole.Write(systemPanel);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[grey]Initializing v{ApplicationVersion}[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async context =>
            {
                var prepareTask = context.AddTask("Preparing terminal", new ProgressTaskSettings { MaxValue = 100 });
                var configurationTask = context.AddTask("Loading configuration", new ProgressTaskSettings { MaxValue = 100 });
                var databaseTask = context.AddTask("Testing database connection", new ProgressTaskSettings { MaxValue = 100 });
                var dashboardTask = context.AddTask("Preparing dashboard", new ProgressTaskSettings { MaxValue = 100 });

                await CompleteStepAsync(prepareTask, "Preparing terminal");
                await CompleteStepAsync(configurationTask, "Loading configuration");

                try
                {
                    await SettingsCommand.TestConnectionAutomaticallyAsync(showProgress: false);
                    CompleteTask(databaseTask, "Database check completed");
                }
                catch
                {
                    databaseTask.Description = "[yellow]Database check unavailable[/]";
                    databaseTask.Value = 100;
                }

                await CompleteStepAsync(dashboardTask, "Preparing dashboard");
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Startup completed. Opening dashboard...[/]");
        await Task.Delay(350);
    }

    private static async Task CompleteStepAsync(ProgressTask task, string description)
    {
        task.Description = $"[cyan]{description}[/]";
        await Task.Yield();
        CompleteTask(task, description);
    }

    private static void CompleteTask(ProgressTask task, string description)
    {
        task.Value = 100;
        task.Description = $"[green]✓ {description}[/]";
    }
}
