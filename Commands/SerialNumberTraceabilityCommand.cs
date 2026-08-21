using CompanyCLI.Models;
using CompanyCLI.Services;
using CompanyCLI.UI;
using Spectre.Console;
using CompanyCLI.Configuration;

namespace CompanyCLI.Commands;

public static class SerialNumberTraceabilityCommand
{
    public static async Task RunAsync()
    {
        var settings = new ServerConnectionSettingsStore().Load(out var settingsError);
        ISerialNumberTraceabilityService service = new DatabaseSerialNumberTraceabilityService(settings);

        while (true)
        {
            Console.Clear();
            TuiComponents.ShowPageHeader(
                "SERIAL NUMBER TRACEABILITY",
                "Dashboard > Serial Number Traceability",
                settingsError is null
                    ? $"Database provider: {settings.Provider}; read-only traceability search"
                    : $"Connection settings error: {settingsError}");

            var inputAction = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]Traceability actions[/]")
                    .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                    .AddChoices("Single Serial Number Search", "Batch Serial Number Search", "Back"));

            if (inputAction == "Back")
            {
                return;
            }

            if (inputAction == "Batch Serial Number Search")
            {
                await RunBatchSearchAsync(service);
                continue;
            }

            var serialNumber = ReadSerialNumberInput();
            if (serialNumber is null)
                return;

            SerialNumberTraceabilityResult? result;
            try
            {
                result = await service.FindAsync(serialNumber);
            }
            catch (Exception exception)
            {
                TuiComponents.ShowError($"Search failed: {exception.Message}");
                TuiComponents.Pause();
                continue;
            }

            if (result is null)
            {
                TuiComponents.ShowError($"Serial Number '{serialNumber}' was not found.");
                TuiComponents.Pause();
                continue;
            }

            ShowResult(result);
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Traceability actions")
                    .AddChoices("Search another Serial Number", "Back"));

            if (action == "Back")
                return;
        }
    }

    private static async Task RunBatchSearchAsync(ISerialNumberTraceabilityService service)
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "BATCH SERIAL NUMBER SEARCH",
            "Dashboard > Serial Number Traceability > Batch Search",
            "Enter up to 50 S/N values separated by commas");

        var serialNumbers = AnsiConsole.Ask<string>(
                "[bold yellow]S/N list[/] [grey](comma-separated, or type B to go back)[/]:")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.Equals(x, "B", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (serialNumbers.Count == 0)
            return;

        if (serialNumbers.Count > 50)
        {
            TuiComponents.ShowError("Batch limit is 50 Serial Numbers per search.");
            TuiComponents.Pause();
            return;
        }

        try
        {
            var results = await service.FindManyAsync(serialNumbers);
            var resultBySerialNumber = results.ToDictionary(x => x.SerialNumber, StringComparer.OrdinalIgnoreCase);
            var table = new Table { Border = TableBorder.Rounded, Expand = true };
            table.AddColumn("Serial Number");
            table.AddColumn("Status");
            table.AddColumn("Location");
            table.AddColumn("Result");

            foreach (var serialNumber in serialNumbers)
            {
                if (resultBySerialNumber.TryGetValue(serialNumber, out var result))
                {
                    table.AddRow(
                        Markup.Escape(serialNumber),
                        Markup.Escape(result.Status),
                        Markup.Escape(result.CurrentLocation),
                        "[green]Found[/]");
                }
                else
                {
                    table.AddRow(Markup.Escape(serialNumber), "-", "-", "[red]Not Found[/]");
                }
            }

            AnsiConsole.Write(table);
            TuiComponents.Pause();
        }
        catch (Exception exception)
        {
            TuiComponents.ShowError($"Batch search failed: {exception.Message}");
            TuiComponents.Pause();
        }
    }

    private static string? ReadSerialNumberInput()
    {
        const int innerWidth = 58;
        const string inputPrefix = " S/N > ";
        var inputWidth = innerWidth - inputPrefix.Length;
        var input = new List<char>();

        Console.WriteLine($"╔{new string('═', innerWidth)}╗");
        Console.WriteLine($"║{" SERIAL NUMBER INPUT".PadRight(innerWidth)}║");
        Console.WriteLine($"║{" Enter an exact S/N to view traceability details.".PadRight(innerWidth)}║");
        var inputLine = Console.CursorTop;
        Console.Write($"║{inputPrefix}{new string(' ', inputWidth)}║");
        Console.WriteLine();
        Console.WriteLine($"╚{new string('═', innerWidth)}╝");

        var inputStartColumn = inputPrefix.Length + 1;
        Console.SetCursorPosition(inputStartColumn, inputLine);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.SetCursorPosition(0, inputLine + 3);
                return new string(input.ToArray()).Trim();
            }

            if (key.Key == ConsoleKey.Escape)
            {
                Console.SetCursorPosition(0, inputLine + 3);
                return null;
            }

            if (key.Key == ConsoleKey.Backspace && input.Count > 0)
                input.RemoveAt(input.Count - 1);
            else if (!char.IsControl(key.KeyChar) && input.Count < inputWidth)
                input.Add(key.KeyChar);
            else
                continue;

            Console.SetCursorPosition(inputStartColumn, inputLine);
            Console.Write(new string(input.ToArray()).PadRight(inputWidth));
            Console.SetCursorPosition(inputStartColumn + input.Count, inputLine);
        }
    }

    private static void ShowResult(SerialNumberTraceabilityResult result)
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "SERIAL NUMBER TRACEABILITY",
            "Dashboard > Serial Number Traceability > Result",
            "Read-only database result");

        var summary = new Table { Border = TableBorder.Rounded, Expand = true };
        summary.AddColumn("Field");
        summary.AddColumn("Value");
        summary.AddRow("Serial Number", Markup.Escape(result.SerialNumber));
        summary.AddRow("Part Number", Markup.Escape(result.PartNumber));
        summary.AddRow("Model", Markup.Escape(result.Model));
        summary.AddRow("Status", Markup.Escape(result.Status));
        summary.AddRow("Manufacture Date", result.ManufactureDate?.ToString("yyyy-MM-dd") ?? "-");
        summary.AddRow("Current Location", Markup.Escape(result.CurrentLocation));
        AnsiConsole.Write(summary);
        AnsiConsole.WriteLine();

        ShowEvents("Test History", result.TestHistory);
        ShowEvents("Repair History", result.RepairHistory);
        ShowEvents("Movement History", result.MovementHistory);
        TuiComponents.Pause();
    }

    private static void ShowEvents(string title, IReadOnlyList<TraceabilityEvent> events)
    {
        var table = new Table
        {
            Border = TableBorder.Rounded,
            Expand = true,
            Title = new TableTitle($"[bold]{Markup.Escape(title)}[/]")
        };
        table.AddColumn("Timestamp");
        table.AddColumn("Event");
        table.AddColumn("Description");
        table.AddColumn("Operator");
        table.AddColumn("Station");

        if (events.Count == 0)
        {
            table.AddRow("-", "-", "No records", "-", "-");
        }
        else
        {
            foreach (var item in events.OrderByDescending(x => x.Timestamp))
            {
                table.AddRow(
                    item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    Markup.Escape(item.EventType),
                    Markup.Escape(item.Description),
                    Markup.Escape(item.Operator),
                    Markup.Escape(item.Station));
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }
}
