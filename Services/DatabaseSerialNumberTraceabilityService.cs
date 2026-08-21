using System.Data.Common;
using CompanyCLI.Configuration;
using CompanyCLI.Models;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace CompanyCLI.Services;

public sealed class DatabaseSerialNumberTraceabilityService : ISerialNumberTraceabilityService
{
    private const int MaximumBatchSize = 50;
    private readonly ServerConnectionSettings settings;

    public DatabaseSerialNumberTraceabilityService(ServerConnectionSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<SerialNumberTraceabilityResult?> FindAsync(
        string serialNumber,
        CancellationToken cancellationToken = default)
    {
        var results = await FindManyAsync([serialNumber], cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<SerialNumberTraceabilityResult>> FindManyAsync(
        IReadOnlyCollection<string> serialNumbers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serialNumbers);

        var requested = serialNumbers
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requested.Length == 0)
            return [];

        if (requested.Length > MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(serialNumbers), $"A maximum of {MaximumBatchSize} Serial Numbers is supported per search.");

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildQuery(requested.Length);
        command.CommandTimeout = Math.Clamp(settings.ConnectionTimeoutSeconds, 5, 120);

        for (var index = 0; index < requested.Length; index++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@sn{index}";
            parameter.Value = requested[index];
            command.Parameters.Add(parameter);
        }

        var results = new List<SerialNumberTraceabilityResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SerialNumberTraceabilityResult
            {
                SerialNumber = reader.GetString(reader.GetOrdinal("SerialNumber")),
                PartNumber = ReadString(reader, "PartNumber"),
                Model = ReadString(reader, "Model"),
                Status = ReadString(reader, "Status"),
                ManufactureDate = ReadDateTime(reader, "ManufactureDate"),
                CurrentLocation = ReadString(reader, "CurrentLocation")
            });
        }

        return results;
    }

    private DbConnection CreateConnection()
    {
        return settings.Provider switch
        {
            DatabaseProvider.SqlServer => new SqlConnection(new SqlServerConnectionService().BuildConnectionString(settings)),
            DatabaseProvider.MySql => new MySqlConnection(new MySqlConnectionService().BuildConnectionString(settings)),
            _ => throw new InvalidOperationException("Unsupported database provider.")
        };
    }

    private string BuildQuery(int parameterCount)
    {
        if (settings.TraceabilityMode == TraceabilityConnectionMode.LinkedServer &&
            settings.Provider != DatabaseProvider.SqlServer)
        {
            throw new InvalidOperationException("Linked Server mode requires SQL Server as the gateway provider.");
        }

        var tableName = settings.TraceabilityMode == TraceabilityConnectionMode.LinkedServer
            ? BuildSqlServerLinkedTableName()
            : settings.Provider == DatabaseProvider.SqlServer
                ? "[dbo].[SerialNumberTraceability]"
                : "`SerialNumberTraceability`";
        var parameters = string.Join(", ", Enumerable.Range(0, parameterCount).Select(index => $"@sn{index}"));

        return $"SELECT SerialNumber, PartNumber, Model, Status, ManufactureDate, CurrentLocation " +
               $"FROM {tableName} WHERE SerialNumber IN ({parameters})";
    }

    private string BuildSqlServerLinkedTableName()
    {
        ValidateSqlIdentifier(settings.LinkedServerName, nameof(settings.LinkedServerName));
        ValidateSqlIdentifier(settings.TraceabilityDatabase, nameof(settings.TraceabilityDatabase));
        ValidateSqlIdentifier(settings.TraceabilitySchema, nameof(settings.TraceabilitySchema));
        ValidateSqlIdentifier(settings.TraceabilityTable, nameof(settings.TraceabilityTable));

        return $"[{settings.LinkedServerName}].[{settings.TraceabilityDatabase}].[{settings.TraceabilitySchema}].[{settings.TraceabilityTable}]";
    }

    private static void ValidateSqlIdentifier(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character =>
                !(char.IsLetterOrDigit(character) || character is '_' or '-' or '.')))
        {
            throw new InvalidOperationException($"{propertyName} contains an invalid SQL identifier.");
        }
    }

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
