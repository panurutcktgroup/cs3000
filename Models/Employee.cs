namespace CompanyCLI.Models;

public sealed class Employee
{
    public int Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
