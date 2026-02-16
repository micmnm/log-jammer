# SampleLog → LogJammer Integration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable seamless ingestion of SampleLog-generated logs into LogJammer via auto-detection and one-click registration.

**Architecture:** New `LogFileDetectService` in Infrastructure reads log files and infers format/field roles. New `POST /api/datasources/detect` endpoint exposes this. Frontend dialog gains a "Detect" button that auto-fills config. SampleLog switches from CLEF to ELK-style JSON + simple text, and gains a `[R]` TUI shortcut to register with LogJammer via API.

**Tech Stack:** .NET 10, C# 13, React 19, MUI 7, TanStack Query 5, Terminal.Gui

---

### Task 1: Change LogFileConnectionConfig from FilePaths (array) to FilePath (singular)

**Files:**
- Modify: `src/LogJammer.Infrastructure/Adapters/LogFile/LogFileConnectionConfig.cs`
- Modify: `src/LogJammer.Infrastructure/Adapters/LogFile/LogFileAdapter.cs`
- Modify: `src/LogJammer.Tests/Unit/Adapters/LogFileAdapterTests.cs`

**Step 1: Update the test helper to use singular filePath**

In `src/LogJammer.Tests/Unit/Adapters/LogFileAdapterTests.cs`, change `MakeConfig` to accept a single string:

```csharp
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
```

Update all test call sites from `MakeConfig([path])` → `MakeConfig(path)` and `MakeConfig(["/nonexistent/file.log"])` → `MakeConfig("/nonexistent/file.log")`.

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/LogJammer.slnx --filter "LogFileAdapterTests"`
Expected: FAIL — `LogFileConnectionConfig.FilePaths` no longer matches the JSON key `filePath`.

**Step 3: Update LogFileConnectionConfig**

In `src/LogJammer.Infrastructure/Adapters/LogFile/LogFileConnectionConfig.cs`:

```csharp
using System.Text.Json.Serialization;

namespace LogJammer.Infrastructure.Adapters.LogFile;

public record LogFileConnectionConfig
{
    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("parseMode")]
    public string ParseMode { get; init; } = "jsonlines"; // "jsonlines" or "regex"

    [JsonPropertyName("regexPattern")]
    public string? RegexPattern { get; init; }

    [JsonPropertyName("timestampField")]
    public string? TimestampField { get; init; }

    [JsonPropertyName("timestampFormat")]
    public string? TimestampFormat { get; init; }

    [JsonPropertyName("levelField")]
    public string? LevelField { get; init; }

    [JsonPropertyName("messageField")]
    public string? MessageField { get; init; }
}
```

**Step 4: Update LogFileAdapter to use singular FilePath**

In `src/LogJammer.Infrastructure/Adapters/LogFile/LogFileAdapter.cs`:
- Replace all `_config.FilePaths` with single-file logic using `_config.FilePath`
- Remove `foreach (var filePath in _config.FilePaths)` loops — just use `_config.FilePath` directly
- `_fileOffsets` dictionary reduces to a single `_fileOffset` long field
- `TestConnectionAsync`: check `File.Exists(_config.FilePath)` directly
- `GetSchemaAsync`: read lines from `_config.FilePath` directly
- `PollErrorsAsync`: read from `_config.FilePath` directly
- `GetSampleRecordsAsync`: read from `_config.FilePath` directly

**Step 5: Run tests to verify they pass**

Run: `dotnet test src/LogJammer.slnx --filter "LogFileAdapterTests"`
Expected: All PASS

**Step 6: Commit**

```bash
git add src/LogJammer.Infrastructure/Adapters/LogFile/LogFileConnectionConfig.cs \
       src/LogJammer.Infrastructure/Adapters/LogFile/LogFileAdapter.cs \
       src/LogJammer.Tests/Unit/Adapters/LogFileAdapterTests.cs
git commit -m "refactor: change LogFileConnectionConfig to single FilePath"
```

---

### Task 2: Create LogFileDetectService

**Files:**
- Create: `src/LogJammer.Infrastructure/Adapters/LogFile/LogFileDetectService.cs`
- Create: `src/LogJammer.Core/Interfaces/ILogFileDetectService.cs`
- Create: `src/LogJammer.Tests/Unit/Adapters/LogFileDetectServiceTests.cs`

**Step 1: Write the failing tests**

In `src/LogJammer.Tests/Unit/Adapters/LogFileDetectServiceTests.cs`:

```csharp
using System.Text.Json;
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
    public async Task Detect_CelfFormat_DetectsAtFields()
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test src/LogJammer.slnx --filter "LogFileDetectServiceTests"`
Expected: FAIL — class does not exist.

**Step 3: Create the detect result models**

In `src/LogJammer.Core/Interfaces/ILogFileDetectService.cs`:

```csharp
namespace LogJammer.Core.Interfaces;

public record DetectResult
{
    public required string DetectedFormat { get; init; }
    public required IReadOnlyList<DetectedField> Fields { get; init; }
    public required IReadOnlyList<Dictionary<string, object?>> SampleRecords { get; init; }
    public required DetectedConfig ProposedConfig { get; init; }
}

public record DetectedField
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? ProposedRole { get; init; } // "Timestamp", "Level", "Message", or null
}

public record DetectedConfig
{
    public required string FilePath { get; init; }
    public required string ParseMode { get; init; }
    public string? TimestampField { get; init; }
    public string? LevelField { get; init; }
    public string? MessageField { get; init; }
    public string? RegexPattern { get; init; }
}

public interface ILogFileDetectService
{
    Task<DetectResult> DetectAsync(string filePath, CancellationToken cancellationToken = default);
}
```

**Step 4: Implement LogFileDetectService**

In `src/LogJammer.Infrastructure/Adapters/LogFile/LogFileDetectService.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using LogJammer.Core.Interfaces;

namespace LogJammer.Infrastructure.Adapters.LogFile;

public class LogFileDetectService(IReadOnlyList<string> allowedDirectories) : ILogFileDetectService
{
    private const int JsonSampleLines = 200;
    private const int TextSampleLines = 20;
    private const int PreviewRecordCount = 5;
    private const double JsonThreshold = 0.8;

    private static readonly string SimpleTimestampLevelRegex =
        @"^(?<timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}[.\d]*)\s+(?<level>\w+)\s+(?<message>.+)$";

    private static readonly HashSet<string> TimestampFieldNames =
        new(StringComparer.OrdinalIgnoreCase) { "timestamp", "@t", "time", "date", "datetime", "eventtime" };

    private static readonly HashSet<string> LevelFieldNames =
        new(StringComparer.OrdinalIgnoreCase) { "level", "@l", "severity", "loglevel", "log_level", "lvl" };

    private static readonly HashSet<string> MessageFieldNames =
        new(StringComparer.OrdinalIgnoreCase) { "message", "@mt", "msg", "text", "body", "log" };

    public async Task<DetectResult> DetectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ValidatePath(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var lines = await ReadLinesAsync(filePath, JsonSampleLines, cancellationToken);

        if (lines.Count == 0)
            throw new InvalidOperationException("File is empty.");

        // Try JSON detection
        var jsonResults = TryParseJsonLines(lines);
        var jsonSuccessRate = lines.Count > 0 ? (double)jsonResults.Count / lines.Count : 0;

        if (jsonSuccessRate >= JsonThreshold)
            return BuildJsonResult(filePath, jsonResults);

        // Fall back to text detection
        return BuildTextResult(filePath, lines);
    }

    private void ValidatePath(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var isAllowed = allowedDirectories.Any(dir =>
            fullPath.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
            throw new UnauthorizedAccessException($"Access denied: path is not in an allowed directory.");
    }

    private static async Task<List<string>> ReadLinesAsync(string filePath, int maxLines, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(filePath);
        while (lines.Count < maxLines && await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }
        return lines;
    }

    private static List<Dictionary<string, object?>> TryParseJsonLines(List<string> lines)
    {
        var results = new List<Dictionary<string, object?>>();
        foreach (var line in lines)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(line);
                if (dict is not null)
                    results.Add(dict);
            }
            catch (JsonException)
            {
                // Not valid JSON — skip
            }
        }
        return results;
    }

    private static DetectResult BuildJsonResult(string filePath, List<Dictionary<string, object?>> records)
    {
        // Union all field names and infer types
        var fieldMap = new Dictionary<string, string>();
        foreach (var record in records)
        {
            foreach (var (key, value) in record)
            {
                if (!fieldMap.ContainsKey(key))
                {
                    fieldMap[key] = value switch
                    {
                        JsonElement el => el.ValueKind switch
                        {
                            JsonValueKind.Number => "Number",
                            JsonValueKind.True or JsonValueKind.False => "Boolean",
                            JsonValueKind.Array => "Array",
                            JsonValueKind.Object => "Object",
                            _ => "String"
                        },
                        _ => "String"
                    };
                }
            }
        }

        // Assign roles
        string? tsField = null, lvlField = null, msgField = null;
        foreach (var name in fieldMap.Keys)
        {
            if (tsField is null && TimestampFieldNames.Contains(name)) tsField = name;
            if (lvlField is null && LevelFieldNames.Contains(name)) lvlField = name;
            if (msgField is null && MessageFieldNames.Contains(name)) msgField = name;
        }

        var fields = fieldMap
            .Select(kv => new DetectedField
            {
                Name = kv.Key,
                Type = kv.Value,
                ProposedRole = kv.Key == tsField ? "Timestamp"
                    : kv.Key == lvlField ? "Level"
                    : kv.Key == msgField ? "Message"
                    : null
            })
            .OrderBy(f => f.Name)
            .ToList();

        return new DetectResult
        {
            DetectedFormat = "jsonlines",
            Fields = fields,
            SampleRecords = records.Take(PreviewRecordCount).ToList(),
            ProposedConfig = new DetectedConfig
            {
                FilePath = filePath,
                ParseMode = "jsonlines",
                TimestampField = tsField,
                LevelField = lvlField,
                MessageField = msgField
            }
        };
    }

    private static DetectResult BuildTextResult(string filePath, List<string> lines)
    {
        var regex = new Regex(SimpleTimestampLevelRegex, RegexOptions.Compiled);

        var sampleRecords = new List<Dictionary<string, object?>>();
        foreach (var line in lines.Take(TextSampleLines))
        {
            var match = regex.Match(line);
            if (match.Success)
            {
                sampleRecords.Add(new Dictionary<string, object?>
                {
                    ["timestamp"] = match.Groups["timestamp"].Value,
                    ["level"] = match.Groups["level"].Value,
                    ["message"] = match.Groups["message"].Value
                });
            }
        }

        var fields = new List<DetectedField>
        {
            new() { Name = "timestamp", Type = "DateTime", ProposedRole = "Timestamp" },
            new() { Name = "level", Type = "String", ProposedRole = "Level" },
            new() { Name = "message", Type = "String", ProposedRole = "Message" }
        };

        return new DetectResult
        {
            DetectedFormat = "text",
            Fields = fields,
            SampleRecords = sampleRecords.Take(PreviewRecordCount).ToList(),
            ProposedConfig = new DetectedConfig
            {
                FilePath = filePath,
                ParseMode = "regex",
                TimestampField = "timestamp",
                LevelField = "level",
                MessageField = "message",
                RegexPattern = SimpleTimestampLevelRegex
            }
        };
    }
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test src/LogJammer.slnx --filter "LogFileDetectServiceTests"`
Expected: All PASS

**Step 6: Commit**

```bash
git add src/LogJammer.Core/Interfaces/ILogFileDetectService.cs \
       src/LogJammer.Infrastructure/Adapters/LogFile/LogFileDetectService.cs \
       src/LogJammer.Tests/Unit/Adapters/LogFileDetectServiceTests.cs
git commit -m "feat: add LogFileDetectService for auto-detecting log format and field roles"
```

---

### Task 3: Add Detect Endpoint to API

**Files:**
- Modify: `src/LogJammer.Api/Controllers/DataSourcesController.cs`
- Create: `src/LogJammer.Api/Dtos/DetectDtos.cs`
- Modify: `src/LogJammer.Api/Program.cs`
- Modify: `src/LogJammer.Infrastructure/Extensions/AdapterServiceExtensions.cs`

**Step 1: Create DTOs**

In `src/LogJammer.Api/Dtos/DetectDtos.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace LogJammer.Api.Dtos;

public record DetectRequest
{
    [Required]
    public required string FilePath { get; init; }
}

public record DetectResponse
{
    public required string DetectedFormat { get; init; }
    public required IReadOnlyList<DetectedFieldDto> Fields { get; init; }
    public required IReadOnlyList<Dictionary<string, object?>> SampleRecords { get; init; }
    public required DetectedConfigDto ProposedConfig { get; init; }
}

public record DetectedFieldDto
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? ProposedRole { get; init; }
}

public record DetectedConfigDto
{
    public required string FilePath { get; init; }
    public required string ParseMode { get; init; }
    public string? TimestampField { get; init; }
    public string? LevelField { get; init; }
    public string? MessageField { get; init; }
    public string? RegexPattern { get; init; }
}
```

**Step 2: Register ILogFileDetectService in DI**

In `src/LogJammer.Infrastructure/Extensions/AdapterServiceExtensions.cs`, add:

```csharp
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Adapters;
using LogJammer.Infrastructure.Adapters.LogFile;
using LogJammer.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LogJammer.Infrastructure.Extensions;

public static class AdapterServiceExtensions
{
    public static IServiceCollection AddDataSourceAdapters(this IServiceCollection services)
    {
        services.AddScoped<IDataSourceRepository, DataSourceRepository>();
        services.AddSingleton<IDataSourceAdapterFactory, DataSourceAdapterFactory>();
        services.AddSingleton<ILogFileDetectService>(sp =>
        {
            var env = sp.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            var allowedDirs = new List<string>
            {
                Path.Combine(env.ContentRootPath, "logs"),
                Path.Combine(env.ContentRootPath, "data")
            };
            return new LogFileDetectService(allowedDirs);
        });
        return services;
    }
}
```

Note: This requires adding `Microsoft.AspNetCore.Hosting.Abstractions` reference to Infrastructure project, or passing the allowed dirs from Program.cs. Check which approach aligns better — if `IWebHostEnvironment` is not available in Infrastructure, register in Program.cs instead.

**Step 3: Add detect endpoint to controller**

In `src/LogJammer.Api/Controllers/DataSourcesController.cs`, add the inject and endpoint:

Update the primary constructor to include `ILogFileDetectService`:

```csharp
public class DataSourcesController(
    IDataSourceService dataSourceService,
    ILogFileDetectService logFileDetectService) : ControllerBase
```

Add the endpoint:

```csharp
[HttpPost("detect")]
public async Task<ActionResult<DetectResponse>> Detect(
    [FromBody] DetectRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var result = await logFileDetectService.DetectAsync(request.FilePath, cancellationToken);
        return Ok(new DetectResponse
        {
            DetectedFormat = result.DetectedFormat,
            Fields = result.Fields.Select(f => new DetectedFieldDto
            {
                Name = f.Name,
                Type = f.Type,
                ProposedRole = f.ProposedRole
            }).ToList(),
            SampleRecords = result.SampleRecords,
            ProposedConfig = new DetectedConfigDto
            {
                FilePath = result.ProposedConfig.FilePath,
                ParseMode = result.ProposedConfig.ParseMode,
                TimestampField = result.ProposedConfig.TimestampField,
                LevelField = result.ProposedConfig.LevelField,
                MessageField = result.ProposedConfig.MessageField,
                RegexPattern = result.ProposedConfig.RegexPattern
            }
        });
    }
    catch (FileNotFoundException)
    {
        return Problem(detail: "File not found.", statusCode: 404);
    }
    catch (UnauthorizedAccessException)
    {
        return Problem(detail: "File path is not in an allowed directory.", statusCode: 403);
    }
    catch (InvalidOperationException ex)
    {
        return Problem(detail: ex.Message, statusCode: 400);
    }
}
```

**Step 4: Build to verify compilation**

Run: `dotnet build src/LogJammer.slnx`
Expected: Build succeeded

**Step 5: Run all tests**

Run: `dotnet test src/LogJammer.slnx`
Expected: All PASS

**Step 6: Commit**

```bash
git add src/LogJammer.Api/Controllers/DataSourcesController.cs \
       src/LogJammer.Api/Dtos/DetectDtos.cs \
       src/LogJammer.Infrastructure/Extensions/AdapterServiceExtensions.cs
git commit -m "feat: add POST /api/datasources/detect endpoint for log format auto-detection"
```

---

### Task 4: Update Frontend — Types and Hook for Detect

**Files:**
- Modify: `src/frontend/src/api/types.ts`
- Modify: `src/frontend/src/api/hooks/useDataSources.ts`

**Step 1: Add detect types**

In `src/frontend/src/api/types.ts`, add at the bottom:

```typescript
export interface DetectRequest {
  filePath: string;
}

export interface DetectedFieldDto {
  name: string;
  type: string;
  proposedRole: string | null;
}

export interface DetectedConfigDto {
  filePath: string;
  parseMode: string;
  timestampField: string | null;
  levelField: string | null;
  messageField: string | null;
  regexPattern: string | null;
}

export interface DetectResponse {
  detectedFormat: string;
  fields: DetectedFieldDto[];
  sampleRecords: Record<string, unknown>[];
  proposedConfig: DetectedConfigDto;
}
```

**Step 2: Add useDetectLogFile hook**

In `src/frontend/src/api/hooks/useDataSources.ts`, add:

```typescript
import type { ..., DetectResponse } from '../types';

export function useDetectLogFile() {
  return useMutation({
    mutationFn: (filePath: string) =>
      api.post<DetectResponse>('/datasources/detect', { filePath }),
  });
}
```

**Step 3: Run frontend tests**

Run: `cd src/frontend && npm test`
Expected: All PASS (no breaking changes to existing tests)

**Step 4: Commit**

```bash
git add src/frontend/src/api/types.ts \
       src/frontend/src/api/hooks/useDataSources.ts
git commit -m "feat: add detect types and useDetectLogFile hook"
```

---

### Task 5: Update Frontend — DataSourceDialog with Detect + Validation

**Files:**
- Modify: `src/frontend/src/components/DataSourceDialog.tsx`
- Modify: `src/frontend/src/components/__tests__/DataSourceDialog.test.tsx`

**Step 1: Update tests first**

In `src/frontend/src/components/__tests__/DataSourceDialog.test.tsx`, add mock for detect and new test cases:

Add to the mock setup:
```typescript
const mockDetectMutate = vi.fn();

vi.mock('../../api/hooks/useDataSources', () => ({
  useCreateDataSource: () => ({ mutate: mockCreateMutate, isPending: false }),
  useUpdateDataSource: () => ({ mutate: mockUpdateMutate, isPending: false }),
  useTestConnection: () => ({ mutate: mockTestMutate, isPending: false }),
  useDetectLogFile: () => ({ mutate: mockDetectMutate, isPending: false }),
}));
```

Add test cases:

```typescript
it('shows LogFile fields with Detect button', async () => {
  const user = userEvent.setup();
  renderWithProviders(
    <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
  );
  const adapterSelect = screen.getAllByText('Elasticsearch')[0];
  await user.click(adapterSelect);
  await user.click(screen.getByText('Log File'));
  expect(screen.getByLabelText('File Path')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Detect' })).toBeInTheDocument();
});

it('disables Create when LogFile mandatory fields are empty', async () => {
  const user = userEvent.setup();
  renderWithProviders(
    <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
  );
  const adapterSelect = screen.getAllByText('Elasticsearch')[0];
  await user.click(adapterSelect);
  await user.click(screen.getByText('Log File'));
  // Name is filled but detect hasn't run
  await user.type(screen.getByLabelText(/Name/), 'Test');
  expect(screen.getByRole('button', { name: 'Create' })).toBeDisabled();
});

it('enables Create after detect fills mandatory fields', async () => {
  const user = userEvent.setup();
  // Mock detect to call onSuccess with response
  mockDetectMutate.mockImplementation((_path: string, opts: { onSuccess: (data: unknown) => void }) => {
    opts.onSuccess({
      detectedFormat: 'jsonlines',
      fields: [
        { name: 'timestamp', type: 'String', proposedRole: 'Timestamp' },
        { name: 'level', type: 'String', proposedRole: 'Level' },
        { name: 'message', type: 'String', proposedRole: 'Message' },
      ],
      sampleRecords: [{ timestamp: '2026-01-01', level: 'ERROR', message: 'test' }],
      proposedConfig: {
        filePath: '/app/logs/test.json',
        parseMode: 'jsonlines',
        timestampField: 'timestamp',
        levelField: 'level',
        messageField: 'message',
        regexPattern: null,
      },
    });
  });

  renderWithProviders(
    <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
  );
  const adapterSelect = screen.getAllByText('Elasticsearch')[0];
  await user.click(adapterSelect);
  await user.click(screen.getByText('Log File'));
  await user.type(screen.getByLabelText(/Name/), 'Test');
  await user.type(screen.getByLabelText('File Path'), '/app/logs/test.json');
  await user.click(screen.getByRole('button', { name: 'Detect' }));

  expect(screen.getByRole('button', { name: 'Create' })).not.toBeDisabled();
});
```

**Step 2: Run tests to verify they fail**

Run: `cd src/frontend && npm test -- --run DataSourceDialog`
Expected: FAIL — Detect button doesn't exist yet, new hooks not imported.

**Step 3: Implement the updated DataSourceDialog**

In `src/frontend/src/components/DataSourceDialog.tsx`, make these changes:

1. Import `useDetectLogFile` alongside other hooks
2. Update `LogFileConfig` interface:
   ```typescript
   interface LogFileConfig {
     filePath: string;
     parseMode: string;
     timestampField: string;
     levelField: string;
     messageField: string;
     regexPattern: string;
   }
   ```
3. Add state for new fields: `lfTimestampField`, `lfLevelField`, `lfMessageField`, `lfDetected` (boolean), `lfSampleRecords`
4. Add "Detect" button next to File Path that calls `detectLogFile.mutate(lfFilePath, { onSuccess: ... })`
5. On success: auto-fill `lfParseMode`, `lfTimestampField`, `lfLevelField`, `lfMessageField`, set `lfDetected = true`, store sample records
6. Change Parse Mode from free text to dropdown (jsonlines/regex)
7. Show Regex Pattern only when `lfParseMode === 'regex'`
8. Show sample records preview table when `lfSampleRecords.length > 0`
9. Update `buildConnectionConfig` to include all fields
10. Update save button disabled logic:
    - For LogFile: disabled unless `name && lfFilePath && lfDetected && lfTimestampField && lfLevelField && lfMessageField && lfParseMode && (lfParseMode !== 'regex' || lfRegexPattern)`
    - For other adapters: existing logic (just `name`)
11. Move "Test Connection" button outside the `isEdit` check — available always
12. Reset new fields in the `useEffect` reset block

**Step 4: Run tests to verify they pass**

Run: `cd src/frontend && npm test -- --run DataSourceDialog`
Expected: All PASS

**Step 5: Commit**

```bash
git add src/frontend/src/components/DataSourceDialog.tsx \
       src/frontend/src/components/__tests__/DataSourceDialog.test.tsx
git commit -m "feat: add Detect button and validation to LogFile DataSourceDialog"
```

---

### Task 6: Switch SampleLog to ELK-style JSON + Simple Text Output

**Files:**
- Modify: `src/SampleLog/Generation/LogGenerator.cs`
- Modify: `src/SampleLog/Models/AppConfig.cs`
- Modify: `src/SampleLog/appsettings.json`
- Modify: `src/SampleLog/UI/MainWindow.cs` (update path references)
- Modify: `run-samplelog.sh`

**Step 1: Update OutputConfig to support output directory change**

In `src/SampleLog/appsettings.json`, change output dir to `../../logs`:

```json
{
  "Output": {
    "Directory": "../../logs",
    "FilePrefix": "sample",
    "RollingSizeMB": 10,
    "MaxFiles": 5
  },
  "Defaults": { ... },
  "LogJammerApi": {
    "BaseUrl": "http://localhost:5050"
  }
}
```

Add `LogJammerApi` config to `AppConfig.cs` (create if no AppConfig aggregator exists — otherwise add to `OutputConfig` or create new record):

In `src/SampleLog/Models/AppConfig.cs`, add:

```csharp
public sealed class LogJammerApiConfig
{
    public string BaseUrl { get; set; } = "http://localhost:5050";
}
```

**Step 2: Rewrite LogGenerator to produce ELK JSON + simple text**

In `src/SampleLog/Generation/LogGenerator.cs`:

- Remove Serilog dependency for file writing (keep for template rendering only or remove entirely)
- Write ELK-style JSON to `{prefix}.json` using `StreamWriter` + `JsonSerializer`
- Write simple text to `{prefix}.log` using `StreamWriter`
- Output format for JSON:
  ```json
  {"timestamp":"2026-02-16T12:34:56.123Z","level":"ERROR","message":"Failed to connect","service":"MyApp.DataService","traceId":"abc","duration":1200}
  ```
- Output format for text:
  ```
  2026-02-16 12:34:56.123 ERROR Failed to connect
  ```
- Expose `JsonFilePath` and `TextFilePath` properties (rename from `LogFilePath`/`RawFilePath`)
- `EmitTemplateInternal`: resolve properties, render message, write both formats
- `EmitPrebaked`: resolve timestamp, write both formats

**Step 3: Update MainWindow references**

In `src/SampleLog/UI/MainWindow.cs`:
- Update `LogFilePath` references to `JsonFilePath`
- Update the log path display label
- Update clipboard copy to copy JSON path

**Step 4: Update run-samplelog.sh**

In `run-samplelog.sh`:
- Change `LOG_DIR` to `./logs` (repo root — the script `cd`s to `src/SampleLog`, but output is now `../../logs` which resolves to repo root)
- Actually, the script does `cd "$(dirname "$0")/src/SampleLog"` then references `./logs`. Since output dir is now `../../logs`, the archive logic should reference `../../logs`:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
LOG_DIR="$SCRIPT_DIR/logs"

if [ -d "$LOG_DIR" ] && [ -n "$(ls -A "$LOG_DIR" 2>/dev/null)" ]; then
    ARCHIVE="$SCRIPT_DIR/src/SampleLog/logs-$(date +%Y%m%d-%H%M%S).zip"
    echo "Archiving existing logs to $ARCHIVE ..."
    zip -jq "$ARCHIVE" "$LOG_DIR"/*
    rm "$LOG_DIR"/*
    echo "Done. Starting SampleLog."
fi

cd "$SCRIPT_DIR/src/SampleLog"
dotnet run
```

**Step 5: Build and verify SampleLog compiles**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded

**Step 6: Commit**

```bash
git add src/SampleLog/Generation/LogGenerator.cs \
       src/SampleLog/Models/AppConfig.cs \
       src/SampleLog/appsettings.json \
       src/SampleLog/UI/MainWindow.cs \
       run-samplelog.sh
git commit -m "feat: switch SampleLog to ELK-style JSON + simple text output"
```

---

### Task 7: Add [R] Register Shortcut to SampleLog TUI

**Files:**
- Modify: `src/SampleLog/UI/MainWindow.cs`
- Modify: `src/SampleLog/Program.cs`

**Step 1: Add HTTP client and config to MainWindow**

Update `MainWindow` constructor to accept `LogJammerApiConfig`:

```csharp
public sealed class MainWindow : Toplevel
{
    private readonly LogGenerator _generator;
    private readonly ScenarioRunner _runner;
    private readonly DefaultsConfig _defaults;
    private readonly LogJammerApiConfig _apiConfig;
    // ... existing fields
```

**Step 2: Add [R] key handler**

In `OnKeyDown`, add:

```csharp
case KeyCode.R:
case KeyCode.R | KeyCode.ShiftMask:
    ShowRegisterDialog();
    return true;
```

**Step 3: Implement ShowRegisterDialog**

```csharp
private void ShowRegisterDialog()
{
    var dialog = new Dialog
    {
        Title = "Register with LogJammer",
        Width = 50,
        Height = 10
    };

    var label = new Label { Text = "Register which log file?", X = 1, Y = 1 };
    var jsonBtn = new Button { Text = "[1] JSON", X = 1, Y = 3 };
    var textBtn = new Button { Text = "[2] Text", X = 14, Y = 3 };
    var bothBtn = new Button { Text = "[3] Both", X = 27, Y = 3 };
    var cancelBtn = new Button { Text = "Cancel" };

    jsonBtn.Accepting += (s, e) => { e.Cancel = true; Application.RequestStop(); RegisterAsync("json"); };
    textBtn.Accepting += (s, e) => { e.Cancel = true; Application.RequestStop(); RegisterAsync("text"); };
    bothBtn.Accepting += (s, e) => { e.Cancel = true; Application.RequestStop(); RegisterAsync("both"); };
    cancelBtn.Accepting += (s, e) => { e.Cancel = true; Application.RequestStop(); };

    dialog.Add(label, jsonBtn, textBtn, bothBtn);
    dialog.AddButton(cancelBtn);
    Application.Run(dialog);
    dialog.Dispose();
}
```

**Step 4: Implement RegisterAsync**

```csharp
private async void RegisterAsync(string mode)
{
    var filesToRegister = new List<(string path, string name)>();

    if (mode is "json" or "both")
        filesToRegister.Add((_generator.JsonFilePath, "SampleLog JSON"));
    if (mode is "text" or "both")
        filesToRegister.Add((_generator.TextFilePath, "SampleLog Text"));

    using var http = new HttpClient { BaseAddress = new Uri(_apiConfig.BaseUrl) };

    foreach (var (path, name) in filesToRegister)
    {
        try
        {
            // Step 1: Detect
            var detectPayload = JsonSerializer.Serialize(new { filePath = path });
            var detectResponse = await http.PostAsync("/api/datasources/detect",
                new StringContent(detectPayload, System.Text.Encoding.UTF8, "application/json"));

            if (!detectResponse.IsSuccessStatusCode)
            {
                var err = await detectResponse.Content.ReadAsStringAsync();
                AddStatusLine($"ERR  [register] Detect failed for {name}: {err}");
                continue;
            }

            var detectResult = await JsonSerializer.DeserializeAsync<JsonElement>(
                await detectResponse.Content.ReadAsStreamAsync());

            var proposedConfig = detectResult.GetProperty("proposedConfig");

            // Step 2: Create data source
            var connectionConfig = JsonSerializer.Serialize(new
            {
                filePath = path,
                parseMode = proposedConfig.GetProperty("parseMode").GetString(),
                timestampField = proposedConfig.GetProperty("timestampField").GetString(),
                levelField = proposedConfig.GetProperty("levelField").GetString(),
                messageField = proposedConfig.GetProperty("messageField").GetString(),
                regexPattern = proposedConfig.TryGetProperty("regexPattern", out var rp) ? rp.GetString() : null
            });

            var createPayload = JsonSerializer.Serialize(new
            {
                name,
                adapterType = "LogFile",
                connectionConfig,
                pollIntervalSeconds = 30,
                enabled = true
            });

            var createResponse = await http.PostAsync("/api/datasources",
                new StringContent(createPayload, System.Text.Encoding.UTF8, "application/json"));

            if (createResponse.IsSuccessStatusCode)
                AddStatusLine($"INF  [register] {name} registered successfully");
            else
            {
                var err = await createResponse.Content.ReadAsStringAsync();
                AddStatusLine($"ERR  [register] Create failed for {name}: {err}");
            }
        }
        catch (Exception ex)
        {
            AddStatusLine($"ERR  [register] {ex.Message}");
        }
    }
}

private void AddStatusLine(string message)
{
    Application.Invoke(() =>
    {
        _pendingLines.Add($"{DateTime.Now:HH:mm:ss} {message}");
        _logDirty = true;
    });
}
```

**Step 5: Update menu display in MainWindow**

Update the menu labels to include `[R]`:
```csharp
var row4 = new Label { Text = "  [4] Correlated failures [C] Copy log path", ... };
var row5 = new Label { Text = "  [R] Register with LogJammer  [Q] Quit", ... };
```

**Step 6: Wire config in Program.cs**

In `src/SampleLog/Program.cs`, add:

```csharp
var apiConfig = new LogJammerApiConfig();
config.GetSection("LogJammerApi").Bind(apiConfig);
```

Pass `apiConfig` to `MainWindow`:
```csharp
var mainWindow = new MainWindow(generator, runner, defaults, apiConfig);
```

**Step 7: Build and verify**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded

**Step 8: Commit**

```bash
git add src/SampleLog/UI/MainWindow.cs \
       src/SampleLog/Program.cs \
       src/SampleLog/Models/AppConfig.cs \
       src/SampleLog/appsettings.json
git commit -m "feat: add [R] Register with LogJammer shortcut to SampleLog TUI"
```

---

### Task 8: Update Spec Files

**Files:**
- Modify: `specs/definition-dto.md`
- Modify: `specs/definition-api.md`

**Step 1: Update definition-dto.md**

Add/update:
- `LogFileConnectionConfig` — document `FilePath` (singular), `ParseMode`, `RegexPattern`, `TimestampField`, `TimestampFormat`, `LevelField`, `MessageField`
- `DetectResult`, `DetectedField`, `DetectedConfig` — new models
- `DetectRequest`, `DetectResponse`, `DetectedFieldDto`, `DetectedConfigDto` — new DTOs

**Step 2: Update definition-api.md**

Add:
- `POST /api/datasources/detect` — request body, response, status codes (200, 400, 403, 404)

Update:
- `POST /api/datasources` — note that LogFile `connectionConfig` now uses `filePath` (singular) with `levelField` and `messageField`

**Step 3: Commit**

```bash
git add specs/definition-dto.md specs/definition-api.md
git commit -m "docs: update spec files with detect endpoint and LogFile config changes"
```

---

### Task 9: Rename Design Doc to Done

**Files:**
- Rename: `specs/plans/samplelog-logjammer-integration.draft.md` → `specs/plans/samplelog-logjammer-integration.done.md`
- Delete: `specs/plans/samplelog-logjammer-integration.toimplement.md`

**Step 1: Rename and clean up**

```bash
git mv specs/plans/samplelog-logjammer-integration.draft.md specs/plans/samplelog-logjammer-integration.done.md
git rm specs/plans/samplelog-logjammer-integration.toimplement.md
git commit -m "docs: mark samplelog-logjammer integration plan as done"
```
