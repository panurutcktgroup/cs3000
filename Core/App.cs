using CompanyCLI.UI;
using Spectre.Console;

namespace CompanyCLI.Core;

public class App
{
    public async Task RunAsync()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        await StartupUI.ShowAsync();
        await DashboardUI.ShowAsync();
    }
}
