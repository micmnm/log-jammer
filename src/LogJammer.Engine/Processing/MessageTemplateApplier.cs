using System.Text.RegularExpressions;

namespace LogJammer.Engine.Processing;

public static partial class MessageTemplateApplier
{
    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex PlaceholderRegex();

    public static string Apply(string? template, Dictionary<string, string> fields)
    {
        if (template is null)
        {
            return template ?? string.Empty;
        }

        return PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return fields.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    public static string Apply(string? template, RawLogEntry entry)
    {
        if (template is null || entry.Fields is null)
        {
            return entry.Message;
        }

        return Apply(template, entry.Fields);
    }
}
