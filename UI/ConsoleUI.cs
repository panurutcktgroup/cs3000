using Spectre.Console;

namespace CompanyCLI.UI;

public static class ConsoleUI
{
    public static void ShowHeader()
    {
        var panel = new Panel(
            Align.Center(
                new Markup(
                    "[bold cyan]PRODUCT MANAGEMENT SYSTEM[/]\n\n" +
                    "[grey]DIACAST DIVISION BPI[/]"
                ),
                VerticalAlignment.Middle
            )
        )
        {
            Border = BoxBorder.Double,
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(
            Align.Center(panel)
        );

        AnsiConsole.WriteLine();
    }

    public static void ShowMessage(
        string title,
        string message)
    {
        Console.Clear();

        var panel = new Panel(
            Align.Center(
                new Markup(
                    $"[bold cyan]{title}[/]\n\n" +
                    $"{message}"
                ),
                VerticalAlignment.Middle
            )
        )
        {
            Header = new PanelHeader(title),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(
            Align.Center(panel)
        );

        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(
            "[grey]Press ENTER to continue...[/]"
        );

        Console.ReadLine();
    }
}