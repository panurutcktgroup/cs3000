namespace CompanyCLI.Models;

public sealed class SerialNumberTraceabilityResult
{
    public string SerialNumber { get; init; } = string.Empty;
    public string PartNumber { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? ManufactureDate { get; init; }
    public string CurrentLocation { get; init; } = string.Empty;
    public IReadOnlyList<TraceabilityEvent> TestHistory { get; init; } = [];
    public IReadOnlyList<TraceabilityEvent> RepairHistory { get; init; } = [];
    public IReadOnlyList<TraceabilityEvent> MovementHistory { get; init; } = [];
}

public sealed class TraceabilityEvent
{
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public string Station { get; init; } = string.Empty;
}
