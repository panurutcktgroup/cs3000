using System;
using System.Threading.Tasks;
using Spectre.Console;

namespace CompanyCLI.UI;

public static partial class ProductControlUI
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private const int DefaultConnectTimeoutSeconds = 60;
    private const int DefaultCommandTimeoutSeconds = 120;

    public static async Task ShowAsync()
    {
        while (true)
        {
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Product Control Management[/]")
                    .PageSize(12)
                    .AddChoices(new[] {
                        "Data Impreg",
                        "WOS-TAG",
                        "2DCR - RESULT",
                        "Back"
                    }));

            if (action == "Back")
                break;

            try
            {
                if (action == "Data Impreg")
                {
                    await ShowDataImpregInteractive();
                }
                else if (action == "WOS-TAG")
                {
                    await ShowWosTagAsync();
                }
                else if (action == "2DCR - RESULT")
                {
                    await Show2DcrResultsAsync();
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }

            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
            AnsiConsole.Clear();
        }
    }
}
