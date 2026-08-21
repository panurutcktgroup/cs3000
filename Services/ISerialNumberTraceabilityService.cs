using CompanyCLI.Models;

namespace CompanyCLI.Services;

public interface ISerialNumberTraceabilityService
{
    Task<SerialNumberTraceabilityResult?> FindAsync(
        string serialNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SerialNumberTraceabilityResult>> FindManyAsync(
        IReadOnlyCollection<string> serialNumbers,
        CancellationToken cancellationToken = default);
}
