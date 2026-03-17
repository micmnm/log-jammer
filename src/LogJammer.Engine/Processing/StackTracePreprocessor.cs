using System.Text.RegularExpressions;

namespace LogJammer.Engine.Processing;

public static partial class StackTracePreprocessor
{
    // Matches lines like:
    //   at MyApp.Services.PaymentService.Process(args) in /app/src/File.cs:line 42
    //   at MyApp.Services.PaymentService.Process(args)
    [GeneratedRegex(@"^\s*at\s+([\w$.]+(?:\.[\w$<>]+)*)\s*\([^)]*\)", RegexOptions.Multiline)]
    private static partial Regex FrameRegex();

    public static Dictionary<string, string> Process(Dictionary<string, string> fields)
    {
        var result = new Dictionary<string, string>(fields.Count, StringComparer.Ordinal);
        foreach (var (key, value) in fields)
        {
            result[key] = IsStackTraceField(key) ? CleanStackTrace(value) : value;
        }
        return result;
    }

    public static bool IsStackTraceField(string fieldName) =>
        fieldName.Contains("stack", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("trace", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("exception", StringComparison.OrdinalIgnoreCase);

    public static string CleanStackTrace(string stackTrace)
    {
        var matches = FrameRegex().Matches(stackTrace);
        if (matches.Count == 0)
        {
            return stackTrace;
        }

        var frames = matches
            .Take(3)
            .Select(m => ShortenMethodName(m.Groups[1].Value));

        return "at " + string.Join(" > ", frames);
    }

    private static string ShortenMethodName(string fullName)
    {
        // Take the last two dot-separated parts: e.g. "MyApp.Services.PaymentService.Process" -> "PaymentService.Process"
        var parts = fullName.Split('.');
        return parts.Length >= 2
            ? parts[^2] + "." + parts[^1]
            : fullName;
    }
}
