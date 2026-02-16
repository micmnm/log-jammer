using System.Text.Json;
using System.Text.RegularExpressions;
using SampleLog.Models;

namespace SampleLog.Generation;

public sealed class LogGenerator : IDisposable
{
    private readonly LogLibrary _library;
    private readonly StreamWriter _jsonWriter;
    private readonly StreamWriter _textWriter;
    private long _emittedCount;

    public long EmittedCount => Interlocked.Read(ref _emittedCount);
    public event Action<string>? OnLogEmitted;
    public LogLibrary Library => _library;
    public string JsonFilePath { get; }
    public string TextFilePath { get; }

    public LogGenerator(LogLibrary library, OutputConfig outputConfig)
    {
        _library = library;

        JsonFilePath = Path.GetFullPath(Path.Combine(outputConfig.Directory, $"{outputConfig.FilePrefix}.json"));
        TextFilePath = Path.GetFullPath(Path.Combine(outputConfig.Directory, $"{outputConfig.FilePrefix}.log"));
        Directory.CreateDirectory(Path.GetDirectoryName(JsonFilePath)!);

        _jsonWriter = new StreamWriter(JsonFilePath, append: true) { AutoFlush = true };
        _textWriter = new StreamWriter(TextFilePath, append: true) { AutoFlush = true };
    }

    public void EmitRandom()
    {
        var template = _library.Templates[Random.Shared.Next(_library.Templates.Count)];
        EmitTemplateInternal(template);
    }

    public void EmitRandomAtLevel(string level)
    {
        var candidates = _library.Templates
            .Where(t => t.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0) return;

        var template = candidates[Random.Shared.Next(candidates.Count)];
        EmitTemplateInternal(template);
    }

    public void EmitTemplate(string templateId)
    {
        var template = _library.Templates.FirstOrDefault(t => t.Id == templateId)
            ?? throw new KeyNotFoundException($"Template '{templateId}' not found in library.");
        EmitTemplateInternal(template);
    }

    public void EmitPrebaked(string id)
    {
        var entry = _library.Prebaked.FirstOrDefault(p => p.Id == id)
            ?? throw new KeyNotFoundException($"Prebaked entry '{id}' not found in library.");

        var now = DateTime.UtcNow;
        var message = entry.Raw.Replace("{{timestamp}}", now.ToString("O"));

        // Write ELK JSON
        var jsonObj = new Dictionary<string, object?>
        {
            ["timestamp"] = now.ToString("O"),
            ["level"] = entry.Level.ToUpperInvariant(),
            ["message"] = message,
            ["source"] = "prebaked"
        };
        _jsonWriter.WriteLine(JsonSerializer.Serialize(jsonObj));

        // Write simple text
        _textWriter.WriteLine($"{now:yyyy-MM-dd HH:mm:ss.fff} {FormatLevelShort(entry.Level),-5} {message}");

        var displayLine = $"{DateTime.Now:HH:mm:ss} {FormatLevelShort(entry.Level),-4} [prebaked] {entry.Id}";
        OnLogEmitted?.Invoke(displayLine);

        Interlocked.Increment(ref _emittedCount);
    }

    public void Dispose()
    {
        _jsonWriter.Dispose();
        _textWriter.Dispose();
    }

    private void EmitTemplateInternal(LogTemplate template)
    {
        var resolvedProperties = new Dictionary<string, object>();

        if (template.Properties is not null)
        {
            foreach (var (key, values) in template.Properties)
            {
                var rawValue = values[Random.Shared.Next(values.Count)];
                resolvedProperties[key] = ResolveJsonElement(rawValue);
            }
        }

        var renderedMessage = RenderMessageTemplate(template.MessageTemplate, resolvedProperties);
        var now = DateTime.UtcNow;
        var level = template.Level.ToUpperInvariant();

        // Build ELK JSON object
        var jsonObj = new Dictionary<string, object?>
        {
            ["timestamp"] = now.ToString("O"),
            ["level"] = level,
            ["message"] = renderedMessage
        };

        if (!string.IsNullOrEmpty(template.SourceContext))
            jsonObj["service"] = template.SourceContext;

        foreach (var (propName, propValue) in resolvedProperties)
            jsonObj[propName] = propValue;

        if (!string.IsNullOrEmpty(template.Exception))
            jsonObj["exception"] = template.Exception;

        _jsonWriter.WriteLine(JsonSerializer.Serialize(jsonObj));

        // Write simple text
        var levelShort = FormatLevelShort(template.Level);
        _textWriter.WriteLine($"{now:yyyy-MM-dd HH:mm:ss.fff} {levelShort,-5} {renderedMessage}");

        // Fire the display event
        var displayLine = $"{DateTime.Now:HH:mm:ss} {FormatLevelShort(template.Level),-4} {renderedMessage}";
        OnLogEmitted?.Invoke(displayLine);

        Interlocked.Increment(ref _emittedCount);
    }

    private static object ResolveJsonElement(object value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number when element.TryGetInt64(out var l) => l,
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => element.GetRawText()
            };
        }

        return value;
    }

    private static string RenderMessageTemplate(string messageTemplate, Dictionary<string, object> properties)
    {
        return Regex.Replace(messageTemplate, @"\{(\w+)\}", match =>
        {
            var propName = match.Groups[1].Value;
            return properties.TryGetValue(propName, out var value)
                ? value?.ToString() ?? ""
                : match.Value;
        });
    }

    private static string FormatLevelShort(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "VERBOSE" => "VRB",
            "DEBUG" => "DBG",
            "INFORMATION" => "INF",
            "WARNING" => "WRN",
            "ERROR" => "ERR",
            "FATAL" => "FTL",
            _ => level[..Math.Min(3, level.Length)].ToUpperInvariant()
        };
    }
}
