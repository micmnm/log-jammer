using FluentAssertions;
using LogJammer.Infrastructure.Adapters.LogFile;

namespace LogJammer.Tests.Unit.Adapters;

public class LogFileDetectServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LogFileDetectService _service;

    public LogFileDetectServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"logjammer-detect-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new LogFileDetectService([_tempDir]);
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

    [Fact]
    public async Task Detect_JsonLines_DetectsFormat()
    {
        var lines = string.Join("\n",
            "{\"timestamp\":\"2026-01-01T00:00:00Z\",\"level\":\"ERROR\",\"message\":\"test1\",\"service\":\"svc\"}",
            "{\"timestamp\":\"2026-01-01T00:01:00Z\",\"level\":\"WARN\",\"message\":\"test2\",\"traceId\":\"abc\"}"
        );
        var path = CreateTempFile(lines);

        var result = await _service.DetectAsync(path);

        result.DetectedFormat.Should().Be("jsonlines");
        result.Fields.Should().Contain(f => f.Name == "timestamp" && f.ProposedRole == "Timestamp");
        result.Fields.Should().Contain(f => f.Name == "level" && f.ProposedRole == "Level");
        result.Fields.Should().Contain(f => f.Name == "message" && f.ProposedRole == "Message");
        result.Fields.Should().Contain(f => f.Name == "service" && f.ProposedRole == null);
        result.Fields.Should().Contain(f => f.Name == "traceId" && f.ProposedRole == null);
        result.SampleRecords.Should().HaveCount(2);
        result.ProposedConfig.ParseMode.Should().Be("jsonlines");
        result.ProposedConfig.TimestampField.Should().Be("timestamp");
        result.ProposedConfig.LevelField.Should().Be("level");
        result.ProposedConfig.MessageField.Should().Be("message");
    }

    [Fact]
    public async Task Detect_ClefFormat_DetectsAtFields()
    {
        var lines = string.Join("\n",
            "{\"@t\":\"2026-01-01T00:00:00Z\",\"@l\":\"Error\",\"@mt\":\"Something failed\",\"SourceContext\":\"MyApp\"}",
            "{\"@t\":\"2026-01-01T00:01:00Z\",\"@mt\":\"Info message\",\"Duration\":123}"
        );
        var path = CreateTempFile(lines);

        var result = await _service.DetectAsync(path);

        result.DetectedFormat.Should().Be("jsonlines");
        result.Fields.Should().Contain(f => f.Name == "@t" && f.ProposedRole == "Timestamp");
        result.Fields.Should().Contain(f => f.Name == "@l" && f.ProposedRole == "Level");
        result.Fields.Should().Contain(f => f.Name == "@mt" && f.ProposedRole == "Message");
        result.ProposedConfig.TimestampField.Should().Be("@t");
        result.ProposedConfig.LevelField.Should().Be("@l");
        result.ProposedConfig.MessageField.Should().Be("@mt");
    }

    [Fact]
    public async Task Detect_TextFormat_DetectsRegex()
    {
        var lines = string.Join("\n",
            "2026-01-01 12:00:00.123 ERROR Something went wrong",
            "2026-01-01 12:00:01.456 WARN Watch out",
            "2026-01-01 12:00:02.789 INFO All is well"
        );
        var path = CreateTempFile(lines);

        var result = await _service.DetectAsync(path);

        result.DetectedFormat.Should().Be("text");
        result.Fields.Should().Contain(f => f.Name == "timestamp" && f.ProposedRole == "Timestamp");
        result.Fields.Should().Contain(f => f.Name == "level" && f.ProposedRole == "Level");
        result.Fields.Should().Contain(f => f.Name == "message" && f.ProposedRole == "Message");
        result.ProposedConfig.ParseMode.Should().Be("regex");
        result.ProposedConfig.RegexPattern.Should().NotBeNullOrEmpty();
        result.SampleRecords.Should().HaveCount(3);
    }

    [Fact]
    public async Task Detect_JsonLines_UnionsFieldsAcross200Lines()
    {
        var lines = new List<string>();
        for (var i = 0; i < 200; i++)
        {
            var extra = i % 2 == 0
                ? ",\"errorCode\":\"E001\""
                : ",\"duration\":123";
            lines.Add($"{{\"timestamp\":\"2026-01-01T00:{i / 60:D2}:{i % 60:D2}Z\",\"level\":\"INFO\",\"message\":\"msg{i}\"{extra}}}");
        }
        var path = CreateTempFile(string.Join("\n", lines));

        var result = await _service.DetectAsync(path);

        result.Fields.Should().Contain(f => f.Name == "errorCode");
        result.Fields.Should().Contain(f => f.Name == "duration");
        result.SampleRecords.Should().HaveCount(5);
    }

    [Fact]
    public async Task Detect_RejectsPathTraversal()
    {
        var act = () => _service.DetectAsync("/etc/passwd");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Detect_FileNotFound_Throws()
    {
        var path = Path.Combine(_tempDir, "nonexistent.log");

        var act = () => _service.DetectAsync(path);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
