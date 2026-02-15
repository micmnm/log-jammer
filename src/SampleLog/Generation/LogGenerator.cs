using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using SampleLog.Models;

namespace SampleLog.Generation;

public sealed class LogGenerator : IDisposable
{
    private readonly LogLibrary _library;
    private readonly Serilog.ILogger _fileLogger;
    private readonly StreamWriter _rawWriter;
    private long _emittedCount;

    public long EmittedCount => Interlocked.Read(ref _emittedCount);
    public event Action<string>? OnLogEmitted;
    public LogLibrary Library => _library;

    public LogGenerator(LogLibrary library, OutputConfig outputConfig)
    {
        _library = library;

        var logFilePath = Path.Combine(outputConfig.Directory, $"{outputConfig.FilePrefix}.txt");
        var rawFilePath = Path.Combine(outputConfig.Directory, $"{outputConfig.FilePrefix}-raw.txt");
        Directory.CreateDirectory(outputConfig.Directory);

        _fileLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                new CompactJsonFormatter(),
                logFilePath,
                rollingInterval: RollingInterval.Infinite,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: outputConfig.RollingSizeMB * 1024L * 1024L,
                retainedFileCountLimit: outputConfig.MaxFiles)
            .CreateLogger();

        _rawWriter = new StreamWriter(rawFilePath, append: true) { AutoFlush = true };
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

        var raw = entry.Raw.Replace("{{timestamp}}", DateTime.UtcNow.ToString("O"));

        _rawWriter.WriteLine(raw);

        var displayLine = $"{DateTime.Now:HH:mm:ss} {FormatLevelShort(entry.Level),-4} [prebaked] {entry.Id}";
        OnLogEmitted?.Invoke(displayLine);

        Interlocked.Increment(ref _emittedCount);
    }

    public void Dispose()
    {
        _rawWriter.Dispose();
        (_fileLogger as IDisposable)?.Dispose();
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

        var level = Enum.Parse<LogEventLevel>(template.Level, ignoreCase: true);

        var logger = _fileLogger;

        if (!string.IsNullOrEmpty(template.SourceContext))
        {
            logger = logger.ForContext("SourceContext", template.SourceContext);
        }

        foreach (var (propName, propValue) in resolvedProperties)
        {
            logger = logger.ForContext(propName, propValue);
        }

        if (!string.IsNullOrEmpty(template.Exception))
        {
            logger.Write(level, new RenderedMessageException(template.Exception), template.MessageTemplate);
        }
        else
        {
            logger.Write(level, template.MessageTemplate);
        }

        // Fire the display event
        var renderedMessage = RenderMessageTemplate(template.MessageTemplate, resolvedProperties);
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

    /// <summary>
    /// Wrapper to pass exception text to Serilog's Write method.
    /// </summary>
    private sealed class RenderedMessageException(string message) : Exception(message)
    {
        public override string ToString() => Message;
    }
}
