using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using Npgsql;

namespace LogJammer.Infrastructure.Adapters.PostgreSql;

public partial class PostgreSqlAdapter : IDataSourceAdapter
{
    private static readonly Regex SafeIdentifierRegex = SafeIdentifier();
    private readonly PostgreSqlConnectionConfig _config;

    public PostgreSqlAdapter(string connectionConfigJson)
    {
        _config = JsonSerializer.Deserialize<PostgreSqlConnectionConfig>(connectionConfigJson)
            ?? throw new ArgumentException("Invalid PostgreSQL connection config JSON.");

        ValidateIdentifier(_config.TableName, "Table name");
        ValidateIdentifier(_config.TimestampColumn, "Timestamp column");
    }

    private static void ValidateIdentifier(string value, string label)
    {
        if (!SafeIdentifierRegex.IsMatch(value))
            throw new ArgumentException($"{label} '{value}' contains invalid characters. Only letters, digits, and underscores are allowed.");
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new NpgsqlConnection(_config.ConnectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = $1";
            cmd.Parameters.AddWithValue(_config.TableName);
            var count = (long)(await cmd.ExecuteScalarAsync(cancellationToken))!;
            sw.Stop();

            if (count == 0)
            {
                return new ConnectionTestResult(false,
                    $"Table '{_config.TableName}' does not exist.",
                    sw.Elapsed);
            }

            return new ConnectionTestResult(true, null, sw.Elapsed,
                new Dictionary<string, object?> { ["tableName"] = _config.TableName });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult(false, ex.Message, sw.Elapsed);
        }
    }

    public async Task<ErrorBatch> PollErrorsAsync(DateTime since, int limit, CancellationToken cancellationToken = default)
    {
        var entries = new List<RawLogEntry>();

        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        // Get total count
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM \"{_config.TableName}\" WHERE \"{_config.TimestampColumn}\" >= $1";
        countCmd.Parameters.AddWithValue(since);
        var total = (int)(long)(await countCmd.ExecuteScalarAsync(cancellationToken))!;

        // Get records
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{_config.TableName}\" WHERE \"{_config.TimestampColumn}\" >= $1 ORDER BY \"{_config.TimestampColumn}\" DESC LIMIT $2";
        cmd.Parameters.AddWithValue(since);
        cmd.Parameters.AddWithValue(limit);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var columnNames = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToList();

        while (await reader.ReadAsync(cancellationToken))
        {
            var fields = new Dictionary<string, object?>();
            var timestamp = DateTime.UtcNow;

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                fields[columnNames[i]] = value;

                if (columnNames[i] == _config.TimestampColumn && value is DateTime dt)
                    timestamp = dt;
            }

            entries.Add(new RawLogEntry(timestamp, fields));
        }

        var sampleRatio = total > 0 ? (double)entries.Count / total : 1.0;
        return new ErrorBatch(entries, total, sampleRatio);
    }

    public async Task<IReadOnlyList<RawLogEntry>> GetSampleRecordsAsync(int count, CancellationToken cancellationToken = default)
    {
        var entries = new List<RawLogEntry>();

        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{_config.TableName}\" ORDER BY \"{_config.TimestampColumn}\" DESC LIMIT $1";
        cmd.Parameters.AddWithValue(count);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var columnNames = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToList();

        while (await reader.ReadAsync(cancellationToken))
        {
            var fields = new Dictionary<string, object?>();
            var timestamp = DateTime.UtcNow;

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                fields[columnNames[i]] = value;

                if (columnNames[i] == _config.TimestampColumn && value is DateTime dt)
                    timestamp = dt;
            }

            entries.Add(new RawLogEntry(timestamp, fields));
        }

        return entries;
    }

    public async Task<IReadOnlyList<FieldDefinition>> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var fields = new List<FieldDefinition>();

        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = $1
            ORDER BY ordinal_position
            """;
        cmd.Parameters.AddWithValue(_config.TableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            fields.Add(new FieldDefinition(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2) == "YES"));
        }

        return fields;
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex SafeIdentifier();
}
