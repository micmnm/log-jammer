using System.Text.RegularExpressions;

namespace LogJammer.Engine.Processing;

public static partial class MessageSanitizer
{
    // 1. URLs: http:// or https:// up to next whitespace
    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex UrlRegex();

    // 2. GUIDs: 8-4-4-4-12 hex groups
    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    // 3a. Unix paths: at least 2 segments starting with /
    //     Not preceded by = (those are handled by key=value step) or word chars (part of URL already replaced)
    [GeneratedRegex(@"(?<![=\w])/(?:[^/\s]+/)+[^/\s]*")]
    private static partial Regex UnixPathRegex();

    // 3b. Windows paths: e.g. C:\foo\bar or C:/foo/bar
    [GeneratedRegex(@"[A-Za-z]:\\(?:[^\\\s]+\\)*[^\\\s]*")]
    private static partial Regex WindowsPathRegex();

    // 4. Key=value args: --flag=<value> where value contains /, digits, or looks like a variable
    //    Don't replace if value is a plain lowercase word (no digits, slashes, or uppercase)
    [GeneratedRegex(@"(--[\w-]+=)(?=[^\s]*[/\d<A-Z_][^\s]*)(\S+)")]
    private static partial Regex KeyValueArgRegex();

    // 5. Standalone numbers: tokens that are purely numeric (with optional decimal) or numeric with common suffixes
    //    Don't match if preceded or followed by a word char (so v2 and log4j are safe)
    //    Numbers in parentheses like (251) should match the number inside
    [GeneratedRegex(@"(?<![a-zA-Z_])(\d+(?:\.\d+)?)\s*(ms|MB|GB|KB|bytes|s|m|M\b)?(?![a-zA-Z_])")]
    private static partial Regex StandaloneNumberRegex();

    public static string Sanitize(string message)
    {
        // Step 1: Replace URLs
        message = UrlRegex().Replace(message, "<url>");

        // Step 2: Replace GUIDs
        message = GuidRegex().Replace(message, "<guid>");

        // Step 3: Replace file paths (Unix then Windows)
        message = UnixPathRegex().Replace(message, "<path>");
        message = WindowsPathRegex().Replace(message, "<path>");

        // Step 4: Replace key=value args where value is complex
        message = KeyValueArgRegex().Replace(message, "$1<value>");

        // Step 5: Replace standalone numbers (with optional suffixes)
        message = StandaloneNumberRegex().Replace(message, m =>
        {
            var suffix = m.Groups[2].Value;
            return string.IsNullOrEmpty(suffix) ? "<num>" : "<num>" + suffix;
        });

        return message;
    }
}
