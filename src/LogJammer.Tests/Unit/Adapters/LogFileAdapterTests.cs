using System.Text.Json;
using FluentAssertions;
using LogJammer.Infrastructure.Adapters.LogFile;

namespace LogJammer.Tests.Unit.Adapters;

public class LogFileAdapterTests : IDisposable
{
    private readonly string _tempDir;

    public LogFileAdapterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"logjammer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CreateTempFile(string content, string fileName = "test.log")
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static string MakeConfig(string filePath, string parseMode = "jsonlines",
        string? regexPattern = null, string? timestampField = null, string? timestampFormat = null)
    {
        return JsonSerializer.Serialize(new
        {
            filePath,
            parseMode,
            regexPattern,
            timestampField,
            timestampFormat
        });
    }

    [Fact]
    public void Constructor_WithValidJsonLinesConfig_Succeeds()
    {
        var path = CreateTempFile("");
        var config = MakeConfig(path);

        var adapter = new LogFileAdapter(config);

        adapter.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithRegexMode_RequiresPattern()
    {
        var path = CreateTempFile("");
        var config = MakeConfig(path, parseMode: "regex");

        var act = () => new LogFileAdapter(config);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*RegexPattern*required*");
    }

    [Fact]
    public void Constructor_WithRegexModeAndPattern_Succeeds()
    {
        var path = CreateTempFile("");
        var config = MakeConfig(path, parseMode: "regex",
            regexPattern: @"(?<timestamp>\S+) (?<level>\S+) (?<message>.+)");

        var adapter = new LogFileAdapter(config);

        adapter.Should().NotBeNull();
    }

    [Fact]
    public async Task TestConnection_WithExistingFiles_ReturnsSuccess()
    {
        var path = CreateTempFile("test content");
        var config = MakeConfig(path);
        var adapter = new LogFileAdapter(config);

        var result = await adapter.TestConnectionAsync();

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Metadata.Should().ContainKey("parseMode");
    }

    [Fact]
    public async Task TestConnection_WithMissingFile_ReturnsFailure()
    {
        var config = MakeConfig("/nonexistent/file.log");
        var adapter = new LogFileAdapter(config);

        var result = await adapter.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task GetSampleRecords_JsonLines_ParsesCorrectly()
    {
        var lines = string.Join("\n",
            "{\"timestamp\":\"2024-01-01T00:00:00Z\",\"level\":\"error\",\"message\":\"test1\"}",
            "{\"timestamp\":\"2024-01-01T00:01:00Z\",\"level\":\"warn\",\"message\":\"test2\"}",
            "{\"timestamp\":\"2024-01-01T00:02:00Z\",\"level\":\"info\",\"message\":\"test3\"}"
        );
        var path = CreateTempFile(lines);
        var config = MakeConfig(path, timestampField: "timestamp");
        var adapter = new LogFileAdapter(config);

        var records = await adapter.GetSampleRecordsAsync(10);

        records.Should().HaveCount(3);
        records[0].Fields.Should().ContainKey("level");
        records[0].Fields.Should().ContainKey("message");
    }

    [Fact]
    public async Task GetSampleRecords_Regex_ParsesNamedGroups()
    {
        var lines = string.Join("\n",
            "2024-01-01T00:00:00Z ERROR test message 1",
            "2024-01-01T00:01:00Z WARN test message 2"
        );
        var path = CreateTempFile(lines);
        var config = MakeConfig(path, parseMode: "regex",
            regexPattern: @"(?<timestamp>\S+)\s+(?<level>\S+)\s+(?<message>.+)",
            timestampField: "timestamp");
        var adapter = new LogFileAdapter(config);

        var records = await adapter.GetSampleRecordsAsync(10);

        records.Should().HaveCount(2);
        records[0].Fields.Should().ContainKey("level");
        records[0].Fields.Should().ContainKey("message");
    }

    [Fact]
    public async Task PollErrors_TracksOffset_DoesNotRereadOldEntries()
    {
        var lines = string.Join("\n",
            "{\"timestamp\":\"2024-01-01T00:00:00Z\",\"message\":\"first\"}",
            "{\"timestamp\":\"2024-01-01T00:01:00Z\",\"message\":\"second\"}"
        );
        var path = CreateTempFile(lines);
        var config = MakeConfig(path, timestampField: "timestamp");
        var adapter = new LogFileAdapter(config);

        // First poll reads all
        var batch1 = await adapter.PollErrorsAsync(DateTime.MinValue, 100);
        batch1.Entries.Should().HaveCount(2);

        // Second poll without new data reads nothing (offset tracked)
        var batch2 = await adapter.PollErrorsAsync(DateTime.MinValue, 100);
        batch2.Entries.Should().BeEmpty();

        // Append new data
        File.AppendAllText(path, "\n{\"timestamp\":\"2024-01-01T00:02:00Z\",\"message\":\"third\"}");

        // Third poll reads only new data
        var batch3 = await adapter.PollErrorsAsync(DateTime.MinValue, 100);
        batch3.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task PollErrors_DetectsFileRotation()
    {
        var lines = string.Join("\n",
            "{\"timestamp\":\"2024-01-01T00:00:00Z\",\"message\":\"original1\"}",
            "{\"timestamp\":\"2024-01-01T00:01:00Z\",\"message\":\"original2\"}"
        );
        var path = CreateTempFile(lines);
        var config = MakeConfig(path, timestampField: "timestamp");
        var adapter = new LogFileAdapter(config);

        // First poll reads all
        var batch1 = await adapter.PollErrorsAsync(DateTime.MinValue, 100);
        batch1.Entries.Should().HaveCount(2);

        // Simulate file rotation - file becomes shorter
        File.WriteAllText(path, "{\"timestamp\":\"2024-01-02T00:00:00Z\",\"message\":\"rotated\"}\n");

        // Should detect rotation and re-read from start
        var batch2 = await adapter.PollErrorsAsync(DateTime.MinValue, 100);
        batch2.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSchema_InfersFieldTypes()
    {
        var lines = string.Join("\n",
            "{\"timestamp\":\"2024-01-01\",\"count\":42,\"active\":true,\"message\":\"test\"}"
        );
        var path = CreateTempFile(lines);
        var config = MakeConfig(path);
        var adapter = new LogFileAdapter(config);

        var schema = await adapter.GetSchemaAsync();

        schema.Should().Contain(f => f.Name == "count" && f.Type == "number");
        schema.Should().Contain(f => f.Name == "active" && f.Type == "boolean");
        schema.Should().Contain(f => f.Name == "message" && f.Type == "string");
    }
}
