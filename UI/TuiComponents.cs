using Spectre.Console;

namespace CompanyCLI.UI;

public static class TuiComponents
{
    public static void ShowPageHeader(string title, string breadcrumb, string hint)
    {
        var header = new Panel(
            Align.Center(
                new Markup($"[bold cyan]{Markup.Escape(title)}[/]"),
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
        AnsiConsole.Write(new Rule($"[grey]{Markup.Escape(breadcrumb)}[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(hint)}[/]");
        AnsiConsole.WriteLine();
    }

    public static void Pause(string message = "Press ENTER to continue...")
    {
        AnsiConsole.MarkupLine($"\n[grey]{Markup.Escape(message)}[/]");
        Console.ReadLine();
    }

    public static void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓ {Markup.Escape(message)}[/]");
    }

    public static void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(message)}[/]");
    }
}
