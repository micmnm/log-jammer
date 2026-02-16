using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;

namespace LogJammer.Infrastructure.Adapters.LogFile;

public class LogFileAdapter : IDataSourceAdapter
{
    private readonly LogFileConnectionConfig _config;
    private readonly Regex? _regex;
    private long _fileOffset;

    public LogFileAdapter(string connectionConfigJson)
    {
        _config = JsonSerializer.Deserialize<LogFileConnectionConfig>(connectionConfigJson)
            ?? throw new ArgumentException("Invalid LogFile connection config JSON.");

        if (_config.ParseMode == "regex")
        {
            if (string.IsNullOrWhiteSpace(_config.RegexPattern))
                throw new ArgumentException("RegexPattern is required when ParseMode is 'regex'.");
            _regex = new Regex(_config.RegexPattern, RegexOptions.Compiled);
        }
    }

    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!File.Exists(_config.FilePath))
            {
                sw.Stop();
                return Task.FromResult(new ConnectionTestResult(false,
                    $"File not found: {_config.FilePath}",
                    sw.Elapsed));
            }

            using var stream = File.Open(_config.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            sw.Stop();

            return Task.FromResult(new ConnectionTestResult(true, null, sw.Elapsed,
                new Dictionary<string, object?>
                {
                    ["parseMode"] = _config.ParseMode
                }));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Task.FromResult(new ConnectionTestResult(false, ex.Message, sw.Elapsed));
        }
    }

    public Task<ErrorBatch> PollErrorsAsync(DateTime since, int limit, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_config.FilePath))
            return Task.FromResult(new ErrorBatch([], 0, 1.0));

        var entries = ReadEntriesFromFile(updateOffset: true);

        var filtered = entries
            .Where(e => e.Timestamp >= since)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList();

        var sampleRatio = entries.Count > 0 ? (double)filtered.Count / entries.Count : 1.0;
        return Task.FromResult(new ErrorBatch(filtered, entries.Count, sampleRatio));
    }

    public Task<IReadOnlyList<RawLogEntry>> GetSampleRecordsAsync(int count, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_config.FilePath))
            return Task.FromResult<IReadOnlyList<RawLogEntry>>([]);

        var entries = ReadEntriesFromFile(updateOffset: false);

        IReadOnlyList<RawLogEntry> result = entries
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<FieldDefinition>> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var fieldNames = new Dictionary<string, string>();

        if (!File.Exists(_config.FilePath))
            return Task.FromResult<IReadOnlyList<FieldDefinition>>([]);

        var lines = File.ReadLines(_config.FilePath).Take(100);
        foreach (var line in lines)
        {
            var fields = ParseLine(line);
            if (fields is null) continue;

            foreach (var (key, value) in fields)
            {
                if (!fieldNames.ContainsKey(key))
                {
                    fieldNames[key] = value switch
                    {
                        JsonElement el => el.ValueKind switch
                        {
                            JsonValueKind.Number => "number",
                            JsonValueKind.True or JsonValueKind.False => "boolean",
                            JsonValueKind.Array => "array",
                            JsonValueKind.Object => "object",
                            _ => "string"
                        },
                        _ => "string"
                    };
                }
            }
        }

        IReadOnlyList<FieldDefinition> result = fieldNames
            .Select(kv => new FieldDefinition(kv.Key, kv.Value, true))
            .OrderBy(f => f.Name)
            .ToList();

        return Task.FromResult(result);
    }

    private List<RawLogEntry> ReadEntriesFromFile(bool updateOffset)
    {
        var entries = new List<RawLogEntry>();

        using var stream = File.Open(_config.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        // Handle file rotation: if file is shorter than stored offset, reset
        if (stream.Length < _fileOffset)
            _fileOffset = 0;

        if (updateOffset && _fileOffset > 0)
            stream.Seek(_fileOffset, SeekOrigin.Begin);

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseLine(line);
            if (fields is null) continue;

            var timestamp = ExtractTimestamp(fields);
            entries.Add(new RawLogEntry(timestamp, fields));
        }

        if (updateOffset)
            _fileOffset = stream.Position;

        return entries;
    }

    private Dictionary<string, object?>? ParseLine(string line)
    {
        if (_config.ParseMode == "jsonlines")
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(line);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        if (_config.ParseMode == "regex" && _regex is not null)
        {
            var match = _regex.Match(line);
            if (!match.Success) return null;

            var fields = new Dictionary<string, object?>();
            foreach (var groupName in _regex.GetGroupNames())
            {
                if (int.TryParse(groupName, out _)) continue; // Skip numbered groups
                fields[groupName] = match.Groups[groupName].Value;
            }

            return fields;
        }

        return null;
    }

    private DateTime ExtractTimestamp(Dictionary<string, object?> fields)
    {
        var tsField = _config.TimestampField ?? "timestamp";

        if (!fields.TryGetValue(tsField, out var value) || value is null)
            return DateTime.UtcNow;

        var valueStr = value switch
        {
            JsonElement el => el.GetRawText().Trim('"'),
            string s => s,
            _ => value.ToString()
        };

        if (valueStr is null) return DateTime.UtcNow;

        if (_config.TimestampFormat is not null)
        {
            if (DateTime.TryParseExact(valueStr, _config.TimestampFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
                return parsed;
        }

        if (DateTime.TryParse(valueStr, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var fallback))
            return fallback;

        return DateTime.UtcNow;
    }
}
