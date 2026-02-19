using System.Text.RegularExpressions;

namespace LogJammer.Infrastructure.Pipeline;

public static partial class FingerprintNormalizer
{
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var result = input;

        // Strip quotes (must run before UUID stripping so quoted UUIDs are cleaned)
        result = QuoteRegex().Replace(result, "");

        // Strip key-value label prefixes (BusMessageId:, CorrelationId:, etc.)
        result = KeyValueLabelRegex().Replace(result, "");

        // Strip HTTP status code prefixes (502:BadGateway:Bad Gateway:)
        result = HttpStatusCodePrefixRegex().Replace(result, "");

        // Strip ISO timestamps (2024-01-15T10:30:45.123Z)
        result = IsoTimestampRegex().Replace(result, "");

        // Strip UUIDs
        result = UuidRegex().Replace(result, "");

        // Strip memory addresses (0x1a2b3c)
        result = MemoryAddressRegex().Replace(result, "");

        // Strip line numbers (:123, line 42)
        result = LineNumberColonRegex().Replace(result, "");
        result = LineNumberWordRegex().Replace(result, "");

        // Strip request/correlation/trace IDs (req-abc123, corr-xyz, trace-456)
        result = RequestIdRegex().Replace(result, "");

        // Collapse whitespace, lowercase, trim
        result = WhitespaceRegex().Replace(result, " ");
        result = result.Trim().ToLowerInvariant();

        return result;
    }

    [GeneratedRegex(@"[""']")]
    private static partial Regex QuoteRegex();

    [GeneratedRegex(@"\b\w*(?:[Ii]d|[Cc]orrelation[Ii]d|[Mm]essage[Ii]d)\s*:\s*")]
    private static partial Regex KeyValueLabelRegex();

    [GeneratedRegex(@"\b\d{3}:[A-Za-z]+(?::[A-Za-z ]+)*:")]
    private static partial Regex HttpStatusCodePrefixRegex();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[\.\d]*Z?")]
    private static partial Regex IsoTimestampRegex();

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex UuidRegex();

    [GeneratedRegex(@"0x[0-9a-fA-F]+")]
    private static partial Regex MemoryAddressRegex();

    [GeneratedRegex(@":\d+")]
    private static partial Regex LineNumberColonRegex();

    [GeneratedRegex(@"\bline\s+\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex LineNumberWordRegex();

    [GeneratedRegex(@"\b(?:req|corr|trace)-[a-zA-Z0-9]+\b")]
    private static partial Regex RequestIdRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
