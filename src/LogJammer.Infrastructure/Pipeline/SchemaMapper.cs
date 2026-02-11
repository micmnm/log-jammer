using System.Text.Json;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;

namespace LogJammer.Infrastructure.Pipeline;

public class SchemaMapper : ISchemaMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MappedLogEntry Map(RawLogEntry entry, string? schemaMappingJson)
    {
        var mapping = ParseMapping(schemaMappingJson);

        var messagePath = mapping.GetValueOrDefault("message", "message");
        var timestampPath = mapping.GetValueOrDefault("timestamp", "timestamp");
        var severityPath = mapping.GetValueOrDefault("severity");
        var stackTracePath = mapping.GetValueOrDefault("stackTrace");

        var mappedFieldPaths = new HashSet<string> { messagePath, timestampPath };
        if (severityPath is not null) mappedFieldPaths.Add(severityPath);
        if (stackTracePath is not null) mappedFieldPaths.Add(stackTracePath);

        var message = ResolveField(entry.Fields, messagePath)?.ToString() ?? string.Empty;
        var timestamp = ResolveTimestamp(entry, timestampPath);
        var severity = ResolveSeverity(ResolveField(entry.Fields, severityPath));
        var stackTrace = ResolveField(entry.Fields, stackTracePath)?.ToString();

        var customFields = new Dictionary<string, object?>();
        CollectUnmappedFields(entry.Fields, mappedFieldPaths, customFields, prefix: null);

        return new MappedLogEntry(message, timestamp, severity, stackTrace, customFields);
    }

    private static Dictionary<string, string> ParseMapping(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>();
    }

    private static object? ResolveField(Dictionary<string, object?> fields, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var segments = path.Split('.');
        object? current = fields;

        foreach (var segment in segments)
        {
            if (current is Dictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(segment, out current))
                    return null;
            }
            else if (current is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(segment, out var prop))
                    current = prop;
                else
                    return null;
            }
            else
            {
                return null;
            }
        }

        if (current is JsonElement jsonEl)
            return JsonElementToObject(jsonEl);

        return current;
    }

    private static DateTime ResolveTimestamp(RawLogEntry entry, string path)
    {
        var value = ResolveField(entry.Fields, path);

        return value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.UtcDateTime,
            string s when DateTime.TryParse(s, out var parsed) => parsed.ToUniversalTime(),
            _ => entry.Timestamp
        };
    }

    private static ErrorSeverity? ResolveSeverity(object? value)
    {
        if (value is null) return null;

        var str = value.ToString()?.Trim().ToLowerInvariant();
        return str switch
        {
            "info" or "information" or "debug" or "trace" => ErrorSeverity.Info,
            "warn" or "warning" => ErrorSeverity.Warning,
            "error" or "critical" or "fatal" or "emergency" or "alert" => ErrorSeverity.Critical,
            _ => null
        };
    }

    private static void CollectUnmappedFields(
        Dictionary<string, object?> fields,
        HashSet<string> mappedPaths,
        Dictionary<string, object?> result,
        string? prefix)
    {
        foreach (var (key, value) in fields)
        {
            var fullPath = prefix is null ? key : $"{prefix}.{key}";
            if (!mappedPaths.Any(p => p == fullPath || p.StartsWith(fullPath + ".")))
            {
                result[fullPath] = value is JsonElement el ? JsonElementToObject(el) : value;
            }
        }
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }
}
