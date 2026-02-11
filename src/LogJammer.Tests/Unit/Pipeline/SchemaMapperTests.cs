using System.Text.Json;
using FluentAssertions;
using LogJammer.Core.Enums;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Pipeline;

namespace LogJammer.Tests.Unit.Pipeline;

public class SchemaMapperTests
{
    private readonly SchemaMapper _mapper = new();

    [Fact]
    public void Map_WithNullSchema_UsesDefaultFieldNames()
    {
        var entry = new RawLogEntry(
            DateTime.UtcNow,
            new Dictionary<string, object?>
            {
                ["message"] = "Something failed",
                ["timestamp"] = "2024-01-15T10:00:00Z",
                ["extra"] = "data"
            });

        var result = _mapper.Map(entry, null);

        result.Message.Should().Be("Something failed");
        result.CustomFields.Should().ContainKey("extra");
    }

    [Fact]
    public void Map_WithCustomSchema_MapsCorrectFields()
    {
        var schema = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["message"] = "log.message",
            ["timestamp"] = "@timestamp",
            ["severity"] = "level",
            ["stackTrace"] = "exception.stacktrace"
        });

        var entry = new RawLogEntry(
            DateTime.UtcNow,
            new Dictionary<string, object?>
            {
                ["log"] = JsonSerializer.SerializeToElement(new { message = "Connection refused" }),
                ["@timestamp"] = "2024-01-15T10:00:00Z",
                ["level"] = "error",
                ["exception"] = JsonSerializer.SerializeToElement(new { stacktrace = "at MyClass.Method()" }),
                ["host"] = "server-1"
            });

        var result = _mapper.Map(entry, schema);

        result.Message.Should().Be("Connection refused");
        result.Severity.Should().Be(ErrorSeverity.Critical);
        result.StackTrace.Should().Be("at MyClass.Method()");
        result.CustomFields.Should().ContainKey("host");
    }

    [Fact]
    public void Map_WithMissingFields_ReturnsEmptyMessage()
    {
        var schema = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["message"] = "nonexistent.field"
        });

        var entry = new RawLogEntry(
            DateTime.UtcNow,
            new Dictionary<string, object?> { ["foo"] = "bar" });

        var result = _mapper.Map(entry, schema);

        result.Message.Should().BeEmpty();
    }

    [Theory]
    [InlineData("error", ErrorSeverity.Critical)]
    [InlineData("critical", ErrorSeverity.Critical)]
    [InlineData("fatal", ErrorSeverity.Critical)]
    [InlineData("warning", ErrorSeverity.Warning)]
    [InlineData("warn", ErrorSeverity.Warning)]
    [InlineData("info", ErrorSeverity.Info)]
    [InlineData("debug", ErrorSeverity.Info)]
    [InlineData("trace", ErrorSeverity.Info)]
    public void Map_SeverityMapping(string input, ErrorSeverity expected)
    {
        var schema = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["severity"] = "level"
        });

        var entry = new RawLogEntry(
            DateTime.UtcNow,
            new Dictionary<string, object?>
            {
                ["message"] = "test",
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["level"] = input
            });

        var result = _mapper.Map(entry, schema);
        result.Severity.Should().Be(expected);
    }

    [Fact]
    public void Map_UnmappedFields_GoToCustomFields()
    {
        var entry = new RawLogEntry(
            DateTime.UtcNow,
            new Dictionary<string, object?>
            {
                ["message"] = "test",
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["host"] = "server-1",
                ["pid"] = 1234
            });

        var result = _mapper.Map(entry, null);

        result.CustomFields.Should().ContainKey("host");
        result.CustomFields.Should().ContainKey("pid");
    }

    [Fact]
    public void Map_FallsBackToEntryTimestamp_WhenFieldMissing()
    {
        var entryTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var entry = new RawLogEntry(
            entryTime,
            new Dictionary<string, object?> { ["message"] = "test" });

        var result = _mapper.Map(entry, null);

        result.Timestamp.Should().Be(entryTime);
    }
}
