using System.Security.Cryptography;
using System.Text;
using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;

namespace LogJammer.Infrastructure.Pipeline;

public class FingerprintCalculator : IFingerprintCalculator
{
    public string ComputeFingerprint(MappedLogEntry entry, IReadOnlyList<FingerprintConfig> configs)
    {
        var orderedConfigs = configs.Count > 0
            ? configs.OrderBy(c => c.Order).ToList()
            : [new FingerprintConfig { FieldName = "message", NormalizeBeforeHash = true, Order = 0 }];

        var parts = new List<string>();

        foreach (var config in orderedConfigs)
        {
            var value = ExtractField(entry, config.FieldName);
            if (value is null)
                continue;

            var text = value.ToString() ?? string.Empty;
            if (config.NormalizeBeforeHash)
                text = FingerprintNormalizer.Normalize(text);

            parts.Add(text);
        }

        var combined = string.Join("|", parts);
        return ComputeSha256(combined);
    }

    private static object? ExtractField(MappedLogEntry entry, string fieldName)
    {
        return fieldName.ToLowerInvariant() switch
        {
            "message" => entry.Message,
            "stacktrace" or "stack_trace" => entry.StackTrace,
            "severity" => entry.Severity?.ToString(),
            "timestamp" => entry.Timestamp.ToString("O"),
            _ => entry.CustomFields.GetValueOrDefault(fieldName)
        };
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
