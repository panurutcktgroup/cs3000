using System;
using System.Linq;
using CompanyCLI.Configuration;
using CompanyCLI.Services;
using CompanyCLI.UI;
using Spectre.Console;

namespace CompanyCLI.Commands;

public static class LinkServerProfilesCommand
{
    public static void Run()
    {
        var store = new LinkServerProfileStore();

        while (true)
        {
            Console.Clear();
            TuiComponents.ShowPageHeader(
                "LINK SERVER PROFILES",
                "Settings > Link Server Profiles",
                "Create, edit, delete profiles (IP only)"
            );

            var profiles = store.LoadAll();

            var table = new Table { Border = TableBorder.Rounded, Expand = true, Title = new TableTitle("[bold cyan]Profiles[/]") };
            table.AddColumn("Name");
            table.AddColumn("Host");

            foreach (var p in profiles)
                table.AddRow(p.Name, p.Host);

            AnsiConsole.Write(table);

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Profile actions")
                    .AddChoices(new[] {
                        "Add Profile",
                        "Edit Profile",
                        "Delete Profile",
                        "Back"
                    }));

            switch (choice)
            {
                case "Add Profile":
                    AddOrEditProfile(store, null);
                    break;
                case "Edit Profile":
                    if (profiles.Count == 0)
                    {
                        TuiComponents.ShowError("No profiles to edit.");
                        TuiComponents.Pause();
                        break;
                    }
                    var pickEdit = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select profile to edit:").AddChoices(profiles.Select(x => x.Name)));
                    AddOrEditProfile(store, profiles.First(x => x.Name == pickEdit));
                    break;
                case "Delete Profile":
                    if (profiles.Count == 0)
                    {
                        TuiComponents.ShowError("No profiles to delete.");
                        TuiComponents.Pause();
                        break;
                    }
                    var pickDel = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select profile to delete:").AddChoices(profiles.Select(x => x.Name)));
                    if (AnsiConsole.Confirm($"Delete profile '{pickDel}'?", false))
                    {
                        store.Remove(pickDel);
                        TuiComponents.ShowSuccess("Profile removed.");
                        TuiComponents.Pause();
                    }
                    break;
                case "Back":
                    return;
            }
        }
    }

    private static void AddOrEditProfile(LinkServerProfileStore store, LinkServerProfile? existing)
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            existing is null ? "ADD LINK PROFILE" : "EDIT LINK PROFILE",
            "Settings > Link Server Profiles",
            "Provide a name and the IP address"
        );

        var name = existing?.Name ?? AnsiConsole.Ask<string>("Profile name (unique):").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TuiComponents.ShowError("Profile name is required.");
            TuiComponents.Pause();
            return;
        }

        var host = existing?.Host ?? AnsiConsole.Ask<string>("Host / IP (e.g. 10.0.0.5):").Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            TuiComponents.ShowError("Host/IP is required.");
            TuiComponents.Pause();
            return;
        }

        // We no longer ask for port — keep default 1433 internally (silent)
        var profile = new LinkServerProfile
        {
            Name = name,
            Host = host,
            Port = 1433
        };

        // Save profile without testing connection
        store.AddOrUpdate(profile);
        TuiComponents.ShowSuccess("Profile saved.");
        TuiComponents.Pause();
    }
}