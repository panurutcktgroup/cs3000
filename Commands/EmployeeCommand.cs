using CompanyCLI.UI;
using CompanyCLI.Models;
using Spectre.Console;

namespace CompanyCLI.Commands;

public static class EmployeeCommand
{
    private static readonly List<Employee> Employees =
    [
        new Employee { Id = 1, Name = "Somchai Jaidee", Department = "IT", IsActive = true },
        new Employee { Id = 2, Name = "Somsak Dee", Department = "Finance", IsActive = true },
        new Employee { Id = 3, Name = "Sarah Smith", Department = "HR", IsActive = true },
        new Employee { Id = 4, Name = "Mike Johnson", Department = "IT", IsActive = false }
    ];

    public static int Count => Employees.Count;

    public static int ActiveCount => Employees.Count(employee => employee.IsActive);

    public static void Run()
    {
        while (true)
        {
            Console.Clear();

            TuiComponents.ShowPageHeader(
                "EMPLOYEE MANAGEMENT",
                "Dashboard > Employee Management",
                "[↑↓] Select   [Enter] Confirm   [B] Back"
            );

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Employee Menu[/]")
                    .AddChoices(
                        "View Employees",
                        "Add Employee",
                        "Edit Employee",
                        "Delete Employee",
                        "Search Employee",
                        "Back"
                    )
            );

            switch (choice)
            {
                case "View Employees":
                    ShowEmployees();
                    break;

                case "Add Employee":
                    AddEmployee();
                    break;

                case "Edit Employee":
                    EditEmployee();
                    break;

                case "Delete Employee":
                    DeleteEmployee();
                    break;

                case "Search Employee":
                    SearchEmployee();
                    break;

                case "Back":
                    return;
            }
        }
    }

    private static void ShowEmployees()
    {
        Console.Clear();

        TuiComponents.ShowPageHeader(
            "EMPLOYEE LIST",
            "Dashboard > Employee Management > View",
            "Showing all employees currently in memory"
        );
        ShowEmployeeTable(Employees, "Employee List");
        TuiComponents.Pause();
    }

    private static void AddEmployee()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "ADD EMPLOYEE",
            "Dashboard > Employee Management > Add",
            "Enter the employee details below"
        );

        var employee = new Employee
        {
            Id = Employees.Count == 0 ? 1 : Employees.Max(item => item.Id) + 1,
            Name = AskRequired("Employee [cyan]name[/]:"),
            Department = AskRequired("Department [cyan]name[/]:"),
            IsActive = AnsiConsole.Confirm("Set employee as [green]active[/]?", true)
        };

        Employees.Add(employee);

        TuiComponents.ShowSuccess($"Employee created successfully. ID: {employee.Id:000}");
        TuiComponents.Pause();
    }

    private static void SearchEmployee()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "SEARCH EMPLOYEES",
            "Dashboard > Employee Management > Search",
            "Search by employee name or department"
        );

        var keyword = AskRequired("Search by name or department:");
        var results = Employees.Where(employee =>
            employee.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            employee.Department.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        ShowEmployeeTable(results, $"Search Results: {Markup.Escape(keyword)}");
        TuiComponents.Pause();
    }

    private static void EditEmployee()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "EDIT EMPLOYEE",
            "Dashboard > Employee Management > Edit",
            "Update the employee details below"
        );

        var id = AnsiConsole.Ask<int>("Enter employee [cyan]ID[/]:");
        var employee = Employees.FirstOrDefault(item => item.Id == id);

        if (employee is null)
        {
            TuiComponents.ShowError($"Employee ID {id:000} was not found.");
            TuiComponents.Pause();
            return;
        }

        employee.Name = AskRequired($"Name [{Markup.Escape(employee.Name)}]:");
        employee.Department = AskRequired($"Department [{Markup.Escape(employee.Department)}]:");
        employee.IsActive = AnsiConsole.Confirm("Employee is [green]active[/]?", employee.IsActive);

        TuiComponents.ShowSuccess("Employee updated successfully.");
        TuiComponents.Pause();
    }

    private static void DeleteEmployee()
    {
        Console.Clear();
        TuiComponents.ShowPageHeader(
            "DELETE EMPLOYEE",
            "Dashboard > Employee Management > Delete",
            "Choose an employee and confirm the delete operation"
        );

        var id = AnsiConsole.Ask<int>("Enter employee [cyan]ID[/]:");
        var employee = Employees.FirstOrDefault(item => item.Id == id);

        if (employee is null)
        {
            TuiComponents.ShowError($"Employee ID {id:000} was not found.");
            TuiComponents.Pause();
            return;
        }

        var displayName = Markup.Escape(employee.Name);
        if (!AnsiConsole.Confirm($"Delete [yellow]{displayName}[/]?", false))
        {
            TuiComponents.ShowError("Delete operation cancelled.");
            TuiComponents.Pause();
            return;
        }

        Employees.Remove(employee);
        TuiComponents.ShowSuccess("Employee deleted successfully.");
        TuiComponents.Pause();
    }

    private static void ShowEmployeeTable(IEnumerable<Employee> employees, string title)
    {
        var table = new Table
        {
            Border = TableBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Title = new TableTitle($"[bold cyan]{title}[/]"),
            Expand = true
        };

        table.AddColumn(new TableColumn("[cyan]ID[/]").Centered());
        table.AddColumn("[cyan]Name[/]");
        table.AddColumn("[cyan]Department[/]");
        table.AddColumn(new TableColumn("[cyan]Status[/]").Centered());

        var employeeList = employees.ToList();
        if (employeeList.Count == 0)
        {
            table.AddRow("-", "[grey]No employees found.[/]", "-", "-");
        }
        else
        {
            foreach (var employee in employeeList)
            {
                table.AddRow(
                    employee.Id.ToString("000"),
                    Markup.Escape(employee.Name),
                    Markup.Escape(employee.Department),
                    employee.IsActive ? "[green]Active[/]" : "[red]Inactive[/]"
                );
            }
        }

        AnsiConsole.Write(table);
    }

    private static string AskRequired(string prompt)
    {
        while (true)
        {
            var value = AnsiConsole.Ask<string>(prompt).Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            AnsiConsole.MarkupLine("[red]This value is required.[/]");
        }
    }

}