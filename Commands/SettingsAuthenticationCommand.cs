using CompanyCLI.Services;
using CompanyCLI.UI;
using Spectre.Console;

namespace CompanyCLI.Commands;

public static class SettingsAuthenticationCommand
{
    private const int MaximumAttempts = 3;

    public static async Task<bool> RunAsync()
    {
        var authenticationService = new LocalAuthenticationService();

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            Console.Clear();
            var credentials = ReadLoginForm();
            if (credentials is null)
                return false;

            try
            {
                if (await authenticationService.VerifyCredentialsAsync(credentials.Value.Username, credentials.Value.Password))
                {
                    TuiComponents.ShowSuccess("Authentication successful.");
                    await Task.Delay(350);
                    return true;
                }

                TuiComponents.ShowError($"Invalid username or password. Attempt {attempt} of {MaximumAttempts}.");
            }
            catch (Exception exception)
            {
                TuiComponents.ShowError($"Authentication service error: {exception.Message}");
            }

            if (attempt < MaximumAttempts)
                TuiComponents.Pause();
        }

        TuiComponents.ShowError("Access denied. Returning to dashboard.");
        TuiComponents.Pause();
        return false;
    }

    private static (string Username, string Password)? ReadLoginForm()
    {
        const int innerWidth = 58;
        const int fieldWidth = 40;
        var left = Math.Max(0, (Console.WindowWidth - innerWidth - 2) / 2);
        var top = Console.CursorTop;

        WriteAt(left, top, $"╔{new string('═', innerWidth)}╗");
        WriteAt(left, top + 1, $"║{CenterText("SETTINGS LOGIN", innerWidth)}║");
        WriteAt(left, top + 2, $"║{" Authorized personnel only".PadRight(innerWidth)}║");
        WriteAt(left, top + 3, $"║{" Username: ".PadRight(innerWidth)}║");
        WriteAt(left, top + 4, $"║{" Password: ".PadRight(innerWidth)}║");
        WriteAt(left, top + 5, $"║{" ESC = Back".PadRight(innerWidth)}║");
        WriteAt(left, top + 6, $"╚{new string('═', innerWidth)}╝");

        var fieldStart = left + 11;
        var username = ReadField(fieldStart, top + 3, fieldWidth, false);
        if (username is null)
            return null;

        var password = ReadField(fieldStart, top + 4, fieldWidth, true);
        if (password is null)
            return null;

        Console.SetCursorPosition(0, top + 8);
        return (username.Trim(), password);
    }

    private static string? ReadField(int left, int top, int width, bool secret)
    {
        var value = new List<char>();
        Console.SetCursorPosition(left, top);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                return new string(value.ToArray());

            if (key.Key == ConsoleKey.Escape)
                return null;

            if (key.Key == ConsoleKey.Backspace && value.Count > 0)
                value.RemoveAt(value.Count - 1);
            else if (!char.IsControl(key.KeyChar) && value.Count < width)
                value.Add(key.KeyChar);
            else
                continue;

            Console.SetCursorPosition(left, top);
            var display = secret
                ? new string('*', value.Count)
                : new string(value.ToArray());
            Console.Write(display.PadRight(width));
            Console.SetCursorPosition(left + value.Count, top);
        }
    }

    private static void WriteAt(int left, int top, string value)
    {
        Console.SetCursorPosition(left, top);
        Console.Write(value);
    }

    private static string CenterText(string value, int width)
    {
        var totalPadding = Math.Max(0, width - value.Length);
        var leftPadding = totalPadding / 2;
        return new string(' ', leftPadding) + value + new string(' ', totalPadding - leftPadding);
    }
}
