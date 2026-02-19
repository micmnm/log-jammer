using FluentAssertions;
using LogJammer.Infrastructure.Pipeline;

namespace LogJammer.Tests.Unit.Pipeline;

public class FingerprintNormalizerTests
{
    [Fact]
    public void Normalize_StripsIsoTimestamps()
    {
        var input = "Error occurred at 2024-01-15T10:30:45.123Z in service";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().NotContain("2024");
        result.Should().Contain("error occurred at");
    }

    [Fact]
    public void Normalize_StripsUuids()
    {
        var input = "Failed for user 550e8400-e29b-41d4-a716-446655440000";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().NotContain("550e8400");
    }

    [Fact]
    public void Normalize_StripsMemoryAddresses()
    {
        var input = "Null reference at 0x1A2B3C4D";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().NotContain("0x1a2b3c4d");
    }

    [Fact]
    public void Normalize_StripsLineNumbers()
    {
        var input = "Exception at MyClass.cs:123 line 42";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().NotContain(":123");
        result.Should().NotContain("line 42");
    }

    [Fact]
    public void Normalize_StripsRequestIds()
    {
        var input = "Request req-abc123 failed with trace-xyz789";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().NotContain("req-abc123");
        result.Should().NotContain("trace-xyz789");
    }

    [Fact]
    public void Normalize_CollapsesWhitespace()
    {
        var input = "Error   in    processing";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().Be("error in processing");
    }

    [Fact]
    public void Normalize_LowercasesAndTrims()
    {
        var input = "  NullReferenceException  ";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().Be("nullreferenceexception");
    }

    [Fact]
    public void Normalize_EmptyInput_ReturnsEmpty()
    {
        FingerprintNormalizer.Normalize("").Should().BeEmpty();
        FingerprintNormalizer.Normalize(null!).Should().BeEmpty();
        FingerprintNormalizer.Normalize("   ").Should().BeEmpty();
    }

    [Fact]
    public void Normalize_CombinedStripping()
    {
        var input = "Error at 2024-01-15T12:00:00Z for user 550e8400-e29b-41d4-a716-446655440000 at 0x1234 line 99 req-abc123";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().Be("error at for user at");
    }

    [Fact]
    public void Normalize_StripsDoubleQuotes()
    {
        var input = "BusMessageId: \"550e8400-e29b-41d4-a716-446655440000\"";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().NotContain("\"");
    }

    [Fact]
    public void Normalize_StripsSingleQuotes()
    {
        var input = "Error in 'UserService'";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().NotContain("'");
    }

    [Fact]
    public void Normalize_StripsKeyValueLabels()
    {
        var input = "BusMessageId: value, BusCorrelationId: value2";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().NotContain("busmessageid");
        result.Should().NotContain("buscorrelationid");
    }

    [Fact]
    public void Normalize_StripsHttpStatusCodePrefixes()
    {
        var input = "502:BadGateway:Bad Gateway:Request failed";
        var result = FingerprintNormalizer.Normalize(input);
        result.Should().NotContain("502");
        result.Should().Contain("request failed");
    }

    [Fact]
    public void Normalize_FormattingVariants_ProduceSameOutput()
    {
        var msg1 = "BusMessageId: 92c850e9-667b-4acb-921b-0a5d9c3560e5, CorrelationId: aa96e498-5632-41ea-9d66-5135f9d87ca1, Request failed with status code BadGateway(Request host is example.ngrok-free.dev)";
        var msg2 = "BusMessageId: \"92c850e9-667b-4acb-921b-0a5d9c3560e5\", BusCorrelationId: \"aa96e498-5632-41ea-9d66-5135f9d87ca1\", \"502:BadGateway:Bad Gateway:Request failed with status code BadGateway(Request host is example.ngrok-free.dev)\"";

        var result1 = FingerprintNormalizer.Normalize(msg1);
        var result2 = FingerprintNormalizer.Normalize(msg2);

        result1.Should().Be(result2);
    }
}
