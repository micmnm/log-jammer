using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Pipeline;

namespace LogJammer.Tests.Unit.Pipeline;

public class FingerprintCalculatorTests
{
    private readonly FingerprintCalculator _calculator = new();

    private static MappedLogEntry MakeEntry(string message, string? stackTrace = null) =>
        new(message, DateTime.UtcNow, ErrorSeverity.Warning, stackTrace, new Dictionary<string, object?>());

    [Fact]
    public void ComputeFingerprint_Deterministic()
    {
        var entry = MakeEntry("NullReferenceException in MyClass");
        var configs = new List<FingerprintConfig>
        {
            new() { FieldName = "message", Order = 0, NormalizeBeforeHash = true }
        };

        var hash1 = _calculator.ComputeFingerprint(entry, configs);
        var hash2 = _calculator.ComputeFingerprint(entry, configs);

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64); // SHA-256 hex
    }

    [Fact]
    public void ComputeFingerprint_DefaultsToMessageField()
    {
        var entry = MakeEntry("Test error message");
        var emptyConfigs = new List<FingerprintConfig>();

        var hash = _calculator.ComputeFingerprint(entry, emptyConfigs);

        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(64);
    }

    [Fact]
    public void ComputeFingerprint_RespectsFieldOrder()
    {
        var entry = MakeEntry("Error msg", "at Stack.Trace()");

        var configsAB = new List<FingerprintConfig>
        {
            new() { FieldName = "message", Order = 0, NormalizeBeforeHash = true },
            new() { FieldName = "stackTrace", Order = 1, NormalizeBeforeHash = true }
        };

        var configsBA = new List<FingerprintConfig>
        {
            new() { FieldName = "stackTrace", Order = 0, NormalizeBeforeHash = true },
            new() { FieldName = "message", Order = 1, NormalizeBeforeHash = true }
        };

        var hashAB = _calculator.ComputeFingerprint(entry, configsAB);
        var hashBA = _calculator.ComputeFingerprint(entry, configsBA);

        hashAB.Should().NotBe(hashBA);
    }

    [Fact]
    public void ComputeFingerprint_NormalizationStabilizesFingerprint()
    {
        var entry1 = MakeEntry("Error at 2024-01-15T10:00:00Z for user 550e8400-e29b-41d4-a716-446655440000");
        var entry2 = MakeEntry("Error at 2025-06-20T14:30:00Z for user 12345678-abcd-ef12-3456-789012345678");

        var configs = new List<FingerprintConfig>
        {
            new() { FieldName = "message", Order = 0, NormalizeBeforeHash = true }
        };

        var hash1 = _calculator.ComputeFingerprint(entry1, configs);
        var hash2 = _calculator.ComputeFingerprint(entry2, configs);

        hash1.Should().Be(hash2); // Same after normalization strips timestamps & UUIDs
    }

    [Fact]
    public void ComputeFingerprint_WithoutNormalization_ProducesDifferentHash()
    {
        var entry1 = MakeEntry("Error at line 10");
        var entry2 = MakeEntry("Error at line 20");

        var configs = new List<FingerprintConfig>
        {
            new() { FieldName = "message", Order = 0, NormalizeBeforeHash = false }
        };

        var hash1 = _calculator.ComputeFingerprint(entry1, configs);
        var hash2 = _calculator.ComputeFingerprint(entry2, configs);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeFingerprint_DifferentMessages_DifferentHashes()
    {
        var entry1 = MakeEntry("NullReferenceException");
        var entry2 = MakeEntry("ArgumentException");

        var configs = new List<FingerprintConfig>
        {
            new() { FieldName = "message", Order = 0, NormalizeBeforeHash = true }
        };

        var hash1 = _calculator.ComputeFingerprint(entry1, configs);
        var hash2 = _calculator.ComputeFingerprint(entry2, configs);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeFingerprint_UsesCustomFields()
    {
        var entry = new MappedLogEntry(
            "Error", DateTime.UtcNow, null, null,
            new Dictionary<string, object?> { ["service"] = "auth" });

        var configs = new List<FingerprintConfig>
        {
            new() { FieldName = "message", Order = 0, NormalizeBeforeHash = true },
            new() { FieldName = "service", Order = 1, NormalizeBeforeHash = false }
        };

        var hash = _calculator.ComputeFingerprint(entry, configs);
        hash.Should().HaveLength(64);
    }
}
