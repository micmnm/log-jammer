using System.Text.Json;
using System.Text.RegularExpressions;
using LogJammer.Core.Interfaces;

namespace LogJammer.Infrastructure.Adapters.LogFile;

public class LogFileDetectService(IReadOnlyList<string> allowedDirectories) : ILogFileDetectService
{
    private const int JsonSampleLines = 200;
    private const int PreviewRecordCount = 5;
    private const double JsonThreshold = 0.8;

    private static readonly string SimpleTimestampLevelRegex =
        @"^(?<timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}[.\d]*)\s+(?<level>\w+)\s+(?<message>.+)$";

    private static readonly HashSet<string> TimestampFieldNames =
        new(StringComparer.OrdinalIgnoreCase) { "timestamp", "@t", "time", "date", "datetime", "eventtime" };

    private static readonly HashSet<string> LevelFieldNames =
        new(StringComparer.OrdinalIgnoreCase) { "level", "@l", "severity", "loglevel", "log_level", "lvl" };

    private static readonly HashSet<string> MessageFieldNames =
        new(StringComparer.OrdinalIgnoreCase) { "message", "@mt", "msg", "text", "body", "log" };

    public async Task<DetectResult> DetectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ValidatePath(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var lines = await ReadLinesAsync(filePath, JsonSampleLines, cancellationToken);

        if (lines.Count == 0)
            throw new InvalidOperationException("File is empty.");

        // Try JSON detection
        var jsonResults = TryParseJsonLines(lines);
        var jsonSuccessRate = lines.Count > 0 ? (double)jsonResults.Count / lines.Count : 0;

        if (jsonSuccessRate >= JsonThreshold)
            return BuildJsonResult(filePath, jsonResults);

        // Fall back to text detection
        return BuildTextResult(filePath, lines);
    }

    private void ValidatePath(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var isAllowed = allowedDirectories.Any(dir =>
            fullPath.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
            throw new UnauthorizedAccessException("Access denied: path is not in an allowed directory.");
    }

    private static async Task<List<string>> ReadLinesAsync(string filePath, int maxLines, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(filePath);
        while (lines.Count < maxLines && await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }
        return lines;
    }

    private static List<Dictionary<string, object?>> TryParseJsonLines(List<string> lines)
    {
        var results = new List<Dictionary<string, object?>>();
        foreach (var line in lines)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(line);
                if (dict is not null)
                    results.Add(dict);
            }
            catch (JsonException)
            {
                // Not valid JSON — skip
            }
        }
        return results;
    }

    private static DetectResult BuildJsonResult(string filePath, List<Dictionary<string, object?>> records)
    {
        // Union all field names and infer types
        var fieldMap = new Dictionary<string, string>();
        foreach (var record in records)
        {
            foreach (var (key, value) in record)
            {
                if (!fieldMap.ContainsKey(key))
                {
                    fieldMap[key] = value switch
                    {
                        JsonElement el => el.ValueKind switch
                        {
                            JsonValueKind.Number => "Number",
                            JsonValueKind.True or JsonValueKind.False => "Boolean",
                            JsonValueKind.Array => "Array",
                            JsonValueKind.Object => "Object",
                            _ => "String"
                        },
                        _ => "String"
                    };
                }
            }
        }

        // Assign roles
        string? tsField = null, lvlField = null, msgField = null;
        foreach (var name in fieldMap.Keys)
        {
            if (tsField is null && TimestampFieldNames.Contains(name)) tsField = name;
            if (lvlField is null && LevelFieldNames.Contains(name)) lvlField = name;
            if (msgField is null && MessageFieldNames.Contains(name)) msgField = name;
        }

        var fields = fieldMap
            .Select(kv => new DetectedField
            {
                Name = kv.Key,
                Type = kv.Value,
                ProposedRole = kv.Key == tsField ? "Timestamp"
                    : kv.Key == lvlField ? "Level"
                    : kv.Key == msgField ? "Message"
                    : null
            })
            .OrderBy(f => f.Name)
            .ToList();

        return new DetectResult
        {
            DetectedFormat = "jsonlines",
            Fields = fields,
            SampleRecords = records.Take(PreviewRecordCount).ToList(),
            ProposedConfig = new DetectedConfig
            {
                FilePath = filePath,
                ParseMode = "jsonlines",
                TimestampField = tsField,
                LevelField = lvlField,
                MessageField = msgField
            }
        };
    }

    private static DetectResult BuildTextResult(string filePath, List<string> lines)
    {
        var regex = new Regex(SimpleTimestampLevelRegex, RegexOptions.Compiled);

        var sampleRecords = new List<Dictionary<string, object?>>();
        foreach (var line in lines)
        {
            var match = regex.Match(line);
            if (match.Success)
            {
                sampleRecords.Add(new Dictionary<string, object?>
                {
                    ["timestamp"] = match.Groups["timestamp"].Value,
                    ["level"] = match.Groups["level"].Value,
                    ["message"] = match.Groups["message"].Value
                });
            }
        }

        var fields = new List<DetectedField>
        {
            new() { Name = "timestamp", Type = "DateTime", ProposedRole = "Timestamp" },
            new() { Name = "level", Type = "String", ProposedRole = "Level" },
            new() { Name = "message", Type = "String", ProposedRole = "Message" }
        };

        return new DetectResult
        {
            DetectedFormat = "text",
            Fields = fields,
            SampleRecords = sampleRecords.Take(PreviewRecordCount).ToList(),
            ProposedConfig = new DetectedConfig
            {
                FilePath = filePath,
                ParseMode = "regex",
                TimestampField = "timestamp",
                LevelField = "level",
                MessageField = "message",
                RegexPattern = SimpleTimestampLevelRegex
            }
        };
    }
}
