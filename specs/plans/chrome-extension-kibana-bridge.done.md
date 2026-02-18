# Chrome Extension Kibana Bridge — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable feeding log data from Kibana to Log Jammer via a Chrome extension when direct ELK access is restricted.

**Architecture:** A Manifest V3 Chrome extension intercepts Kibana Discover queries, lets the user subscribe to them on a schedule, re-runs them through Kibana's authenticated proxy, and pushes results to a new Log Jammer ingest endpoint. The backend adds a `KibanaProxy` adapter type (receive-only) and a `POST /api/ingest/{dataSourceId}` endpoint that feeds into the existing fingerprint → classify pipeline.

**Tech Stack:** .NET 10 / C# 13 (backend), TypeScript 5.9 + Vite + React 19 + MUI 7 (extension popup), Chrome Extension Manifest V3

**Design doc:** `docs/plans/2026-02-18-chrome-extension-kibana-bridge-design.md`

---

## Phase A: Backend — KibanaProxy Adapter & Ingest Endpoint

### Task 1: Add KibanaProxy to AdapterType enum

**Files:**
- Modify: `src/LogJammer.Core/Enums/AdapterType.cs`

**Step 1: Add enum value**

```csharp
namespace LogJammer.Core.Enums;

public enum AdapterType
{
    Elasticsearch,
    LogFile,
    PostgreSql,
    KibanaProxy
}
```

**Step 2: Build to verify no compile errors**

Run: `dotnet build src/LogJammer.slnx`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/LogJammer.Core/Enums/AdapterType.cs
git commit -m "feat: add KibanaProxy to AdapterType enum"
```

---

### Task 2: Skip KibanaProxy in DataSourcePollingManager and handle in AdapterFactory

**Files:**
- Modify: `src/LogJammer.Infrastructure/Pipeline/DataSourcePollingManager.cs:46`
- Modify: `src/LogJammer.Infrastructure/Adapters/DataSourceAdapterFactory.cs:14`

**Step 1: Filter out KibanaProxy in polling manager**

In `DataSourcePollingManager.ReconcileAsync`, change line 46:

```csharp
// Before:
var enabledIds = dataSources.Where(ds => ds.Enabled).Select(ds => ds.Id).ToHashSet();

// After:
var enabledIds = dataSources
    .Where(ds => ds.Enabled && ds.AdapterType != Core.Enums.AdapterType.KibanaProxy)
    .Select(ds => ds.Id).ToHashSet();
```

**Step 2: Handle KibanaProxy in adapter factory**

In `DataSourceAdapterFactory.CreateAdapter`, the existing `_ => throw` default already covers `KibanaProxy`. No change needed — the factory will throw `ArgumentOutOfRangeException` if someone accidentally tries to create a KibanaProxy adapter, which is correct (push-only, no adapter needed).

**Step 3: Build**

Run: `dotnet build src/LogJammer.slnx`
Expected: Build succeeded

**Step 4: Run existing tests to verify no regressions**

Run: `dotnet test src/LogJammer.slnx`
Expected: All tests pass

**Step 5: Commit**

```bash
git add src/LogJammer.Infrastructure/Pipeline/DataSourcePollingManager.cs
git commit -m "feat: skip KibanaProxy sources in polling manager"
```

---

### Task 3: Extract ingestion pipeline from DataSourcePollingService

The core entry-processing logic (lines 87-135 of `DataSourcePollingService.cs`) needs to be reusable by both the polling service and the new ingest endpoint. Extract into a shared service.

**Files:**
- Create: `src/LogJammer.Core/Interfaces/ILogIngestionPipeline.cs`
- Create: `src/LogJammer.Infrastructure/Pipeline/LogIngestionPipeline.cs`
- Modify: `src/LogJammer.Infrastructure/Pipeline/DataSourcePollingService.cs` (use extracted service)
- Modify: `src/LogJammer.Infrastructure/Extensions/PipelineServiceExtensions.cs` (register)
- Test: `src/LogJammer.Tests/Unit/Pipeline/LogIngestionPipelineTests.cs`

**Step 1: Create the interface**

```csharp
// src/LogJammer.Core/Interfaces/ILogIngestionPipeline.cs
using LogJammer.Core.Entities;
using LogJammer.Core.Models;

namespace LogJammer.Core.Interfaces;

public record IngestionResult(int Accepted, int Duplicates, int Failed);

public interface ILogIngestionPipeline
{
    Task<IngestionResult> ProcessEntriesAsync(
        DataSource dataSource,
        IReadOnlyList<RawLogEntry> entries,
        double sampleRatio,
        CancellationToken cancellationToken = default);
}
```

**Step 2: Implement the pipeline**

```csharp
// src/LogJammer.Infrastructure/Pipeline/LogIngestionPipeline.cs
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class LogIngestionPipeline(
    ISchemaMapper schemaMapper,
    IFingerprintCalculator fingerprintCalculator,
    IKnownErrorRepository knownErrorRepo,
    IErrorOccurrenceRepository occurrenceRepo,
    LogJammerDbContext dbContext,
    ILogger<LogIngestionPipeline> logger) : ILogIngestionPipeline
{
    public async Task<IngestionResult> ProcessEntriesAsync(
        DataSource dataSource,
        IReadOnlyList<RawLogEntry> entries,
        double sampleRatio,
        CancellationToken cancellationToken = default)
    {
        var fingerprintConfigs = dataSource.FingerprintConfigs.ToList();
        int accepted = 0;
        int duplicates = 0;
        int failed = 0;

        foreach (var entry in entries)
        {
            try
            {
                var mapped = schemaMapper.Map(entry, dataSource.SchemaMapping);
                var fingerprint = fingerprintCalculator.ComputeFingerprint(mapped, fingerprintConfigs);

                var knownError = await knownErrorRepo.GetByFingerprintHashAsync(fingerprint, cancellationToken);
                knownError ??= await knownErrorRepo.GetByFingerprintAliasAsync(fingerprint, cancellationToken);

                if (knownError is null)
                {
                    knownError = await knownErrorRepo.AddAsync(new KnownError
                    {
                        FingerprintHash = fingerprint,
                        RepresentativeMessage = mapped.Message,
                        RepresentativeStackTrace = mapped.StackTrace,
                        Severity = mapped.Severity ?? ErrorSeverity.Warning,
                        Status = ErrorStatus.Active,
                        FirstSeen = mapped.Timestamp,
                        LastSeen = mapped.Timestamp,
                        TotalOccurrences = 1,
                        DataSourceId = dataSource.Id
                    }, cancellationToken);

                    dbContext.ClassificationQueue.Add(new ClassificationQueueItem
                    {
                        KnownErrorId = knownError.Id
                    });
                    await dbContext.SaveChangesAsync(cancellationToken);

                    accepted++;
                }
                else
                {
                    knownError.LastSeen = mapped.Timestamp > knownError.LastSeen ? mapped.Timestamp : knownError.LastSeen;
                    knownError.TotalOccurrences++;
                    await knownErrorRepo.UpdateAsync(knownError, cancellationToken);
                    duplicates++;
                }

                await occurrenceRepo.UpsertWindowAsync(
                    knownError.Id, mapped.Timestamp, mapped.Timestamp.AddMinutes(5),
                    sampleRatio, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process entry for data source {DataSourceId}", dataSource.Id);
                failed++;
            }
        }

        return new IngestionResult(accepted, duplicates, failed);
    }
}
```

**Step 3: Register in DI**

In `src/LogJammer.Infrastructure/Extensions/PipelineServiceExtensions.cs`, add after line 24 (after FingerprintCalculator):

```csharp
services.AddScoped<ILogIngestionPipeline, LogIngestionPipeline>();
```

**Step 4: Refactor DataSourcePollingService to use the extracted pipeline**

Replace lines 85-135 of `DataSourcePollingService.cs` with:

```csharp
var ingestionPipeline = scope.ServiceProvider.GetRequiredService<ILogIngestionPipeline>();
await ingestionPipeline.ProcessEntriesAsync(dataSource, batch.Entries, batch.SampleRatio, cancellationToken);
```

And remove the individual service resolutions that are no longer needed (schemaMapper, fingerprintCalculator, knownErrorRepo, occurrenceRepo). Keep `dataSourceRepo` and `adapterFactory`.

The refactored `ExecutePollCycleAsync`:

```csharp
private async Task<int> ExecutePollCycleAsync(CancellationToken cancellationToken)
{
    using var scope = _scopeFactory.CreateScope();
    var dataSourceRepo = scope.ServiceProvider.GetRequiredService<IDataSourceRepository>();
    var adapterFactory = scope.ServiceProvider.GetRequiredService<IDataSourceAdapterFactory>();
    var ingestionPipeline = scope.ServiceProvider.GetRequiredService<ILogIngestionPipeline>();

    var dataSource = await dataSourceRepo.GetByIdAsync(_dataSourceId, cancellationToken);
    if (dataSource is null || !dataSource.Enabled)
    {
        _logger.LogWarning("Data source {DataSourceId} not found or disabled, skipping poll", _dataSourceId);
        return 30;
    }

    var adapter = adapterFactory.CreateAdapter(dataSource.AdapterType, dataSource.ConnectionConfig);
    var batch = await adapter.PollErrorsAsync(_lastPollTime, dataSource.SamplingBudget, cancellationToken);

    if (batch.Entries.Count == 0)
    {
        _logger.LogDebug("No new entries for data source {DataSourceId}", _dataSourceId);
        _lastPollTime = DateTime.UtcNow;
        return dataSource.PollIntervalSeconds;
    }

    _logger.LogInformation("Processing {Count} entries for data source {DataSourceId}", batch.Entries.Count, _dataSourceId);

    await ingestionPipeline.ProcessEntriesAsync(dataSource, batch.Entries, batch.SampleRatio, cancellationToken);

    _lastPollTime = DateTime.UtcNow;
    return dataSource.PollIntervalSeconds;
}
```

**Step 5: Build and run tests**

Run: `dotnet build src/LogJammer.slnx`
Expected: Build succeeded

Run: `dotnet test src/LogJammer.slnx`
Expected: All existing tests pass

**Step 6: Write pipeline integration test**

```csharp
// src/LogJammer.Tests/Unit/Pipeline/LogIngestionPipelineTests.cs
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using LogJammer.Tests.Integration;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Tests.Unit.Pipeline;

public class LogIngestionPipelineTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private LogJammer.Infrastructure.Data.LogJammerDbContext _context = null!;

    public async Task InitializeAsync()
    {
        Skip.IfNot(TestDatabaseProvider.IsDockerAvailable(), "Docker is not available");
        await _fixture.InitializeAsync();
        _context = _fixture.CreateDbContext();
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context is not null) await _context.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    [SkippableFact]
    public async Task ProcessEntries_NewEntry_CreatesKnownErrorAndQueuesClassification()
    {
        // Arrange
        var dataSource = new DataSource
        {
            Name = "Test KibanaProxy",
            AdapterType = AdapterType.KibanaProxy,
            ConnectionConfig = "{}",
            Enabled = true,
            PollIntervalSeconds = 60,
            SamplingBudget = 500
        };
        _context.DataSources.Add(dataSource);
        await _context.SaveChangesAsync();

        var pipeline = CreatePipeline();
        var entries = new List<RawLogEntry>
        {
            new(DateTime.UtcNow, new Dictionary<string, object?>
            {
                ["message"] = "NullReferenceException in UserService",
                ["level"] = "Error"
            })
        };

        // Act
        var result = await pipeline.ProcessEntriesAsync(dataSource, entries, 1.0);

        // Assert
        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, result.Duplicates);

        var knownErrors = await _context.KnownErrors.Where(ke => ke.DataSourceId == dataSource.Id).ToListAsync();
        Assert.Single(knownErrors);

        var queueItems = await _context.ClassificationQueue.Where(q => q.KnownErrorId == knownErrors[0].Id).ToListAsync();
        Assert.Single(queueItems);
    }

    [SkippableFact]
    public async Task ProcessEntries_DuplicateEntry_IncrementsOccurrences()
    {
        // Arrange
        var dataSource = new DataSource
        {
            Name = "Test KibanaProxy 2",
            AdapterType = AdapterType.KibanaProxy,
            ConnectionConfig = "{}",
            Enabled = true,
            PollIntervalSeconds = 60,
            SamplingBudget = 500
        };
        _context.DataSources.Add(dataSource);
        await _context.SaveChangesAsync();

        var pipeline = CreatePipeline();
        var entries = new List<RawLogEntry>
        {
            new(DateTime.UtcNow, new Dictionary<string, object?>
            {
                ["message"] = "Timeout connecting to database",
                ["level"] = "Error"
            })
        };

        // First push
        await pipeline.ProcessEntriesAsync(dataSource, entries, 1.0);

        // Second push (same message = same fingerprint)
        var result = await pipeline.ProcessEntriesAsync(dataSource, entries, 1.0);

        // Assert
        Assert.Equal(0, result.Accepted);
        Assert.Equal(1, result.Duplicates);

        var knownError = await _context.KnownErrors
            .Where(ke => ke.DataSourceId == dataSource.Id)
            .SingleAsync();
        Assert.Equal(2, knownError.TotalOccurrences);
    }

    private LogIngestionPipeline CreatePipeline()
    {
        var schemaMapper = new SchemaMapper();
        var fingerprintCalculator = new FingerprintCalculator();
        var knownErrorRepo = new KnownErrorRepository(_context);
        var occurrenceRepo = new ErrorOccurrenceRepository(_context);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<LogIngestionPipeline>.Instance;
        return new LogIngestionPipeline(schemaMapper, fingerprintCalculator, knownErrorRepo, occurrenceRepo, _context, logger);
    }
}
```

**Step 7: Run the new test**

Run: `dotnet test src/LogJammer.slnx --filter "LogIngestionPipelineTests"`
Expected: 2 tests pass (or skip if Docker unavailable)

**Step 8: Commit**

```bash
git add src/LogJammer.Core/Interfaces/ILogIngestionPipeline.cs \
        src/LogJammer.Infrastructure/Pipeline/LogIngestionPipeline.cs \
        src/LogJammer.Infrastructure/Pipeline/DataSourcePollingService.cs \
        src/LogJammer.Infrastructure/Extensions/PipelineServiceExtensions.cs \
        src/LogJammer.Tests/Unit/Pipeline/LogIngestionPipelineTests.cs
git commit -m "refactor: extract LogIngestionPipeline from DataSourcePollingService"
```

---

### Task 4: Create Ingest DTOs, Service, and Controller

**Files:**
- Create: `src/LogJammer.Api/Dtos/IngestDtos.cs`
- Create: `src/LogJammer.Core/Interfaces/IIngestService.cs`
- Create: `src/LogJammer.Api/Services/IngestService.cs`
- Create: `src/LogJammer.Api/Controllers/IngestController.cs`
- Modify: `src/LogJammer.Api/Program.cs` (register service)
- Test: `src/LogJammer.Tests/Integration/Api/IngestControllerTests.cs`

**Step 1: Create DTOs**

```csharp
// src/LogJammer.Api/Dtos/IngestDtos.cs
using System.ComponentModel.DataAnnotations;

namespace LogJammer.Api.Dtos;

public record IngestRequest
{
    [Required]
    [MinLength(1)]
    public required IReadOnlyList<IngestEntry> Entries { get; init; }
}

public record IngestEntry
{
    [Required]
    public DateTime Timestamp { get; init; }

    [Required]
    public required Dictionary<string, object?> Fields { get; init; }
}

public record IngestResponse
{
    public int Accepted { get; init; }
    public int Duplicates { get; init; }
    public int Failed { get; init; }
}
```

**Step 2: Create service interface**

```csharp
// src/LogJammer.Core/Interfaces/IIngestService.cs
namespace LogJammer.Core.Interfaces;

public interface IIngestService
{
    Task<(int Accepted, int Duplicates, int Failed)> IngestAsync(
        Guid dataSourceId,
        IReadOnlyList<(DateTime Timestamp, Dictionary<string, object?> Fields)> entries,
        CancellationToken cancellationToken = default);
}
```

**Step 3: Create service implementation**

```csharp
// src/LogJammer.Api/Services/IngestService.cs
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;

namespace LogJammer.Api.Services;

public interface IIngestService
{
    Task<(int Accepted, int Duplicates, int Failed)> IngestAsync(
        Guid dataSourceId,
        IReadOnlyList<(DateTime Timestamp, Dictionary<string, object?> Fields)> entries,
        CancellationToken cancellationToken = default);
}

public class IngestService(
    IDataSourceRepository dataSourceRepo,
    ILogIngestionPipeline ingestionPipeline) : IIngestService
{
    public async Task<(int Accepted, int Duplicates, int Failed)> IngestAsync(
        Guid dataSourceId,
        IReadOnlyList<(DateTime Timestamp, Dictionary<string, object?> Fields)> entries,
        CancellationToken cancellationToken = default)
    {
        var dataSource = await dataSourceRepo.GetByIdAsync(dataSourceId, cancellationToken);
        if (dataSource is null)
            throw new KeyNotFoundException($"Data source {dataSourceId} not found");

        if (dataSource.AdapterType != AdapterType.KibanaProxy)
            throw new InvalidOperationException($"Data source {dataSourceId} is not a KibanaProxy source. Only KibanaProxy sources accept pushed data.");

        var rawEntries = entries
            .Select(e => new RawLogEntry(e.Timestamp, e.Fields))
            .ToList();

        var result = await ingestionPipeline.ProcessEntriesAsync(dataSource, rawEntries, 1.0, cancellationToken);
        return (result.Accepted, result.Duplicates, result.Failed);
    }
}
```

Note: Define `IIngestService` in the same file as the implementation (Api/Services pattern) rather than in Core/Interfaces. This keeps it simple — the interface is only used by the controller in the same project.

**Step 4: Create controller**

```csharp
// src/LogJammer.Api/Controllers/IngestController.cs
using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController(IIngestService ingestService) : ControllerBase
{
    [HttpPost("{dataSourceId:guid}")]
    public async Task<ActionResult<IngestResponse>> Ingest(
        Guid dataSourceId,
        [FromBody] IngestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = request.Entries
                .Select(e => (e.Timestamp, e.Fields))
                .ToList();

            var (accepted, duplicates, failed) = await ingestService.IngestAsync(
                dataSourceId, entries, cancellationToken);

            return Ok(new IngestResponse
            {
                Accepted = accepted,
                Duplicates = duplicates,
                Failed = failed
            });
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: 404);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }
    }
}
```

**Step 5: Register in Program.cs**

Add after the other scoped service registrations:

```csharp
builder.Services.AddScoped<IIngestService, IngestService>();
```

Also add the `using LogJammer.Api.Services;` if not already present (it should be, since other services are registered there).

**Step 6: Build**

Run: `dotnet build src/LogJammer.slnx`
Expected: Build succeeded

**Step 7: Write controller integration tests**

```csharp
// src/LogJammer.Tests/Integration/Api/IngestControllerTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using FluentAssertions;
using NSubstitute;

namespace LogJammer.Tests.Integration.Api;

public class IngestControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly IIngestService _ingestService;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IngestControllerTests()
    {
        _client = _factory.CreateClient();
        _ingestService = _factory.IngestService;
    }

    [Fact]
    public async Task Ingest_ValidRequest_ReturnsOkWithCounts()
    {
        // Arrange
        var dataSourceId = Guid.NewGuid();
        _ingestService.IngestAsync(
            dataSourceId,
            Arg.Any<IReadOnlyList<(DateTime, Dictionary<string, object?>)>>(),
            Arg.Any<CancellationToken>())
            .Returns((5, 2, 0));

        var request = new IngestRequest
        {
            Entries =
            [
                new IngestEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Fields = new Dictionary<string, object?> { ["message"] = "test error" }
                }
            ]
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/ingest/{dataSourceId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestResponse>(JsonOptions);
        body!.Accepted.Should().Be(5);
        body.Duplicates.Should().Be(2);
        body.Failed.Should().Be(0);
    }

    [Fact]
    public async Task Ingest_DataSourceNotFound_Returns404()
    {
        // Arrange
        var dataSourceId = Guid.NewGuid();
        _ingestService.IngestAsync(
            dataSourceId,
            Arg.Any<IReadOnlyList<(DateTime, Dictionary<string, object?>)>>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new KeyNotFoundException("Not found"));

        var request = new IngestRequest
        {
            Entries =
            [
                new IngestEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Fields = new Dictionary<string, object?> { ["message"] = "test" }
                }
            ]
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/ingest/{dataSourceId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ingest_NonKibanaProxySource_Returns400()
    {
        var dataSourceId = Guid.NewGuid();
        _ingestService.IngestAsync(
            dataSourceId,
            Arg.Any<IReadOnlyList<(DateTime, Dictionary<string, object?>)>>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Not a KibanaProxy source"));

        var request = new IngestRequest
        {
            Entries =
            [
                new IngestEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Fields = new Dictionary<string, object?> { ["message"] = "test" }
                }
            ]
        };

        var response = await _client.PostAsJsonAsync($"/api/ingest/{dataSourceId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public void Dispose() => _factory.Dispose();
}
```

**Step 8: Add IIngestService mock to TestWebApplicationFactory**

In `src/LogJammer.Tests/TestWebApplicationFactory.cs`, add:

```csharp
public IIngestService IngestService { get; } = Substitute.For<IIngestService>();
```

And in `ConfigureWebHost.ConfigureServices`:

```csharp
services.RemoveAll<IIngestService>();
services.AddSingleton(IngestService);
```

**Step 9: Run all tests**

Run: `dotnet test src/LogJammer.slnx`
Expected: All tests pass (including new IngestController tests)

**Step 10: Commit**

```bash
git add src/LogJammer.Api/Dtos/IngestDtos.cs \
        src/LogJammer.Api/Services/IngestService.cs \
        src/LogJammer.Api/Controllers/IngestController.cs \
        src/LogJammer.Api/Program.cs \
        src/LogJammer.Tests/TestWebApplicationFactory.cs \
        src/LogJammer.Tests/Integration/Api/IngestControllerTests.cs
git commit -m "feat: add POST /api/ingest endpoint for KibanaProxy push ingestion"
```

---

### Task 5: Update specs and frontend types

**Files:**
- Modify: `specs/definition-dto.md` (add IngestionResult, IngestRequest/Response)
- Modify: `specs/definition-api.md` (add POST /api/ingest/{dataSourceId})
- Modify: `src/frontend/src/api/types.ts` (add KibanaProxy to AdapterType union)

**Step 1: Update definition-dto.md**

Add under the Models section:

```markdown
### IngestionResult
- `Accepted` (int): Number of new unique errors ingested
- `Duplicates` (int): Number of entries matching existing error groups
- `Failed` (int): Number of entries that failed processing

### IngestRequest
- `Entries` (IngestEntry[]): Required, min 1 entry

### IngestEntry
- `Timestamp` (DateTime): Required
- `Fields` (Dictionary<string, object?>): Required, raw key-value pairs

### IngestResponse
- `Accepted` (int)
- `Duplicates` (int)
- `Failed` (int)
```

Add `KibanaProxy` to the `AdapterType` enum documentation.

**Step 2: Update definition-api.md**

Add:

```markdown
### Ingest (Push)
| Method | Endpoint | Status |
|--------|----------|--------|
| POST | /api/ingest/{dataSourceId} | ✅ Implemented |

**POST /api/ingest/{dataSourceId}**
- Body: IngestRequest (entries array)
- 200: IngestResponse with accepted/duplicates/failed counts
- 400: DataSource is not KibanaProxy type
- 404: DataSource not found
```

**Step 3: Update frontend types**

In `src/frontend/src/api/types.ts`, update the AdapterType:

```typescript
export type AdapterType = 'Elasticsearch' | 'LogFile' | 'PostgreSql' | 'KibanaProxy';
```

**Step 4: Commit**

```bash
git add specs/definition-dto.md specs/definition-api.md src/frontend/src/api/types.ts
git commit -m "docs: update specs for KibanaProxy adapter and ingest endpoint"
```

---

## Phase B: Chrome Extension — Project Setup

### Task 6: Scaffold Chrome extension project

**Files:**
- Create: `src/chrome-extension/package.json`
- Create: `src/chrome-extension/tsconfig.json`
- Create: `src/chrome-extension/vite.config.ts`
- Create: `src/chrome-extension/manifest.json`
- Create: `src/chrome-extension/src/shared/types.ts`

**Step 1: Create package.json**

```json
{
  "name": "logjammer-kibana-bridge",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite build --watch",
    "build": "tsc -b && vite build",
    "test": "vitest",
    "test:run": "vitest run"
  },
  "dependencies": {
    "react": "^19.2.0",
    "react-dom": "^19.2.0",
    "@mui/material": "^7.3.8",
    "@mui/icons-material": "^7.3.8",
    "@emotion/react": "^11.14.0",
    "@emotion/styled": "^11.14.1"
  },
  "devDependencies": {
    "typescript": "~5.9.3",
    "vite": "^7.3.1",
    "vitest": "^4.0.18",
    "@vitejs/plugin-react": "^5.1.1",
    "@testing-library/react": "^16.3.2",
    "@testing-library/jest-dom": "^6.9.1",
    "@testing-library/user-event": "^14.6.1",
    "jsdom": "^28.0.0",
    "@types/react": "^19.2.0",
    "@types/react-dom": "^19.2.0",
    "@types/chrome": "^0.0.300"
  }
}
```

**Step 2: Create tsconfig.json**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "verbatimModuleSyntax": true,
    "moduleDetection": "force",
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "types": ["chrome", "vitest/globals"]
  },
  "include": ["src"]
}
```

**Step 3: Create vite.config.ts**

```typescript
/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist',
    emptyDir: true,
    rollupOptions: {
      input: {
        popup: resolve(__dirname, 'src/popup/popup.html'),
        'service-worker': resolve(__dirname, 'src/background/service-worker.ts'),
        'content-script': resolve(__dirname, 'src/content/kibana-interceptor.ts'),
      },
      output: {
        entryFileNames: '[name].js',
        chunkFileNames: 'chunks/[name]-[hash].js',
        assetFileNames: 'assets/[name]-[hash][extname]',
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
});
```

**Step 4: Create manifest.json**

```json
{
  "manifest_version": 3,
  "name": "Log Jammer — Kibana Bridge",
  "version": "0.1.0",
  "description": "Capture Kibana Discover queries and feed log data to Log Jammer",
  "permissions": [
    "storage",
    "alarms",
    "activeTab"
  ],
  "host_permissions": [
    "http://localhost:5050/*",
    "http://localhost:8080/*"
  ],
  "background": {
    "service_worker": "service-worker.js",
    "type": "module"
  },
  "content_scripts": [
    {
      "matches": ["<all_urls>"],
      "js": ["content-script.js"],
      "run_at": "document_start"
    }
  ],
  "action": {
    "default_popup": "src/popup/popup.html",
    "default_title": "Log Jammer Kibana Bridge"
  },
  "icons": {
    "16": "icons/icon16.png",
    "48": "icons/icon48.png",
    "128": "icons/icon128.png"
  }
}
```

Note: `host_permissions` for Kibana URLs will be added by the user via the extension settings (or they can manually edit manifest.json). The default includes localhost Log Jammer URLs.

**Step 5: Create shared types**

```typescript
// src/chrome-extension/src/shared/types.ts

export interface CapturedQuery {
  id: string;
  kibanaUrl: string;
  proxyEndpoint: string;
  method: string;
  indexPattern: string;
  queryDsl: Record<string, unknown>;
  summary: string;
  capturedAt: string; // ISO 8601
}

export interface Subscription {
  id: string;
  queryId: string;
  dataSourceId: string;
  name: string;
  pollIntervalMinutes: number;
  lastPollAt: string | null;
  lastError: string | null;
  status: 'active' | 'paused' | 'error';
}

export interface ExtensionSettings {
  logJammerUrl: string;
  maxCapturedQueries: number;
}

export const DEFAULT_SETTINGS: ExtensionSettings = {
  logJammerUrl: 'http://localhost:5050',
  maxCapturedQueries: 50,
};

export interface IngestEntry {
  timestamp: string;
  fields: Record<string, unknown>;
}

export interface IngestResponse {
  accepted: number;
  duplicates: number;
  failed: number;
}
```

**Step 6: Create test setup**

```typescript
// src/chrome-extension/src/test/setup.ts
import '@testing-library/jest-dom/vitest';
```

**Step 7: Create placeholder icon**

```bash
mkdir -p src/chrome-extension/icons
# Create a simple 128x128 placeholder PNG (can be replaced with real icon later)
```

**Step 8: Install dependencies**

Run: `cd src/chrome-extension && npm install`
Expected: Dependencies installed successfully

**Step 9: Verify build**

Run: `cd src/chrome-extension && npx tsc --noEmit`
Expected: No TypeScript errors

**Step 10: Commit**

```bash
git add src/chrome-extension/
git commit -m "feat: scaffold Chrome extension project (MV3, Vite, React, MUI)"
```

---

### Task 7: Create the Kibana query parser utility

This utility converts ES query DSL into human-readable summaries.

**Files:**
- Create: `src/chrome-extension/src/shared/kibana-query-parser.ts`
- Test: `src/chrome-extension/src/shared/__tests__/kibana-query-parser.test.ts`

**Step 1: Write the failing tests**

```typescript
// src/chrome-extension/src/shared/__tests__/kibana-query-parser.test.ts
import { summarizeQuery, extractIndexPattern } from '../kibana-query-parser';

describe('kibana-query-parser', () => {
  describe('summarizeQuery', () => {
    it('summarizes a simple match query', () => {
      const query = {
        query: { match: { 'log.level': 'ERROR' } }
      };
      expect(summarizeQuery(query)).toContain('log.level:ERROR');
    });

    it('summarizes a bool query with must clauses', () => {
      const query = {
        query: {
          bool: {
            must: [
              { match: { 'log.level': 'ERROR' } },
              { match: { 'service.name': 'api-gateway' } }
            ]
          }
        }
      };
      const summary = summarizeQuery(query);
      expect(summary).toContain('log.level:ERROR');
      expect(summary).toContain('service.name:api-gateway');
    });

    it('summarizes a range filter', () => {
      const query = {
        query: {
          bool: {
            filter: [
              { range: { '@timestamp': { gte: 'now-15m', lte: 'now' } } }
            ]
          }
        }
      };
      expect(summarizeQuery(query)).toContain('@timestamp');
    });

    it('returns fallback for empty query', () => {
      expect(summarizeQuery({})).toBe('(all documents)');
    });

    it('summarizes query_string queries', () => {
      const query = {
        query: { query_string: { query: 'status:500 AND path:/api/*' } }
      };
      expect(summarizeQuery(query)).toBe('status:500 AND path:/api/*');
    });
  });

  describe('extractIndexPattern', () => {
    it('extracts index from Kibana bsearch URL', () => {
      const url = '/internal/bsearch';
      const body = { params: { index: 'logs-*' } };
      expect(extractIndexPattern(url, body)).toBe('logs-*');
    });

    it('returns unknown for unrecognized format', () => {
      expect(extractIndexPattern('/some/url', {})).toBe('unknown');
    });
  });
});
```

**Step 2: Run tests to verify they fail**

Run: `cd src/chrome-extension && npx vitest run --reporter=verbose`
Expected: FAIL — modules not found

**Step 3: Implement the parser**

```typescript
// src/chrome-extension/src/shared/kibana-query-parser.ts

export function summarizeQuery(queryDsl: Record<string, unknown>): string {
  const query = queryDsl.query as Record<string, unknown> | undefined;
  if (!query) return '(all documents)';

  const parts: string[] = [];

  if ('query_string' in query) {
    const qs = query.query_string as Record<string, unknown>;
    return (qs.query as string) || '(all documents)';
  }

  if ('match' in query) {
    parts.push(summarizeMatch(query.match as Record<string, unknown>));
  }

  if ('bool' in query) {
    const bool = query.bool as Record<string, unknown>;
    for (const clause of ['must', 'filter', 'should'] as const) {
      const items = bool[clause];
      if (Array.isArray(items)) {
        for (const item of items) {
          parts.push(summarizeClause(item as Record<string, unknown>));
        }
      }
    }
  }

  return parts.filter(Boolean).join(' AND ') || '(all documents)';
}

function summarizeMatch(match: Record<string, unknown>): string {
  return Object.entries(match)
    .map(([field, value]) => `${field}:${value}`)
    .join(' AND ');
}

function summarizeClause(clause: Record<string, unknown>): string {
  if ('match' in clause) {
    return summarizeMatch(clause.match as Record<string, unknown>);
  }
  if ('match_phrase' in clause) {
    return summarizeMatch(clause.match_phrase as Record<string, unknown>);
  }
  if ('term' in clause) {
    return summarizeMatch(clause.term as Record<string, unknown>);
  }
  if ('range' in clause) {
    const range = clause.range as Record<string, unknown>;
    const field = Object.keys(range)[0];
    const bounds = range[field] as Record<string, unknown>;
    const parts = Object.entries(bounds).map(([op, val]) => `${op}:${val}`);
    return `${field}[${parts.join(',')}]`;
  }
  if ('query_string' in clause) {
    const qs = clause.query_string as Record<string, unknown>;
    return (qs.query as string) || '';
  }
  return '';
}

export function extractIndexPattern(
  url: string,
  body: Record<string, unknown>
): string {
  // Kibana bsearch format
  if (body.params && typeof body.params === 'object') {
    const params = body.params as Record<string, unknown>;
    if (typeof params.index === 'string') return params.index;
  }

  // Direct ES search with index in URL
  const urlMatch = url.match(/\/([^/]+)\/_(?:search|msearch)/);
  if (urlMatch) return urlMatch[1];

  return 'unknown';
}
```

**Step 4: Run tests**

Run: `cd src/chrome-extension && npx vitest run --reporter=verbose`
Expected: All tests pass

**Step 5: Commit**

```bash
git add src/chrome-extension/src/shared/kibana-query-parser.ts \
        src/chrome-extension/src/shared/__tests__/kibana-query-parser.test.ts
git commit -m "feat(extension): add Kibana query DSL parser and summarizer"
```

---

### Task 8: Create chrome.storage wrapper utility

**Files:**
- Create: `src/chrome-extension/src/utils/storage.ts`
- Test: `src/chrome-extension/src/utils/__tests__/storage.test.ts`

**Step 1: Write failing tests**

```typescript
// src/chrome-extension/src/utils/__tests__/storage.test.ts
import { StorageManager } from '../storage';
import type { CapturedQuery, Subscription, ExtensionSettings } from '../../shared/types';
import { DEFAULT_SETTINGS } from '../../shared/types';

// Mock chrome.storage.local
const mockStorage: Record<string, unknown> = {};
const chromeMock = {
  storage: {
    local: {
      get: vi.fn((keys: string[]) =>
        Promise.resolve(
          Object.fromEntries(keys.map(k => [k, mockStorage[k]]))
        )
      ),
      set: vi.fn((items: Record<string, unknown>) => {
        Object.assign(mockStorage, items);
        return Promise.resolve();
      }),
    },
  },
};
vi.stubGlobal('chrome', chromeMock);

describe('StorageManager', () => {
  beforeEach(() => {
    Object.keys(mockStorage).forEach(k => delete mockStorage[k]);
    vi.clearAllMocks();
  });

  it('returns default settings when none stored', async () => {
    const settings = await StorageManager.getSettings();
    expect(settings).toEqual(DEFAULT_SETTINGS);
  });

  it('saves and retrieves settings', async () => {
    const custom: ExtensionSettings = { logJammerUrl: 'http://example.com', maxCapturedQueries: 100 };
    await StorageManager.saveSettings(custom);
    const settings = await StorageManager.getSettings();
    expect(settings.logJammerUrl).toBe('http://example.com');
  });

  it('adds and retrieves captured queries', async () => {
    const query: CapturedQuery = {
      id: 'q1',
      kibanaUrl: 'https://kibana.corp.com',
      proxyEndpoint: '/internal/bsearch',
      method: 'POST',
      indexPattern: 'logs-*',
      queryDsl: { query: { match_all: {} } },
      summary: '(all documents)',
      capturedAt: new Date().toISOString(),
    };

    await StorageManager.addCapturedQuery(query);
    const queries = await StorageManager.getCapturedQueries();
    expect(queries).toHaveLength(1);
    expect(queries[0].id).toBe('q1');
  });

  it('limits stored queries to maxCapturedQueries', async () => {
    // Store 3 queries with limit of 2
    await StorageManager.saveSettings({ ...DEFAULT_SETTINGS, maxCapturedQueries: 2 });

    for (let i = 0; i < 3; i++) {
      await StorageManager.addCapturedQuery({
        id: `q${i}`,
        kibanaUrl: 'https://kibana.corp.com',
        proxyEndpoint: '/internal/bsearch',
        method: 'POST',
        indexPattern: 'logs-*',
        queryDsl: {},
        summary: `query ${i}`,
        capturedAt: new Date().toISOString(),
      });
    }

    const queries = await StorageManager.getCapturedQueries();
    expect(queries.length).toBeLessThanOrEqual(2);
  });

  it('saves and retrieves subscriptions', async () => {
    const sub: Subscription = {
      id: 's1',
      queryId: 'q1',
      dataSourceId: 'ds-guid',
      name: 'Prod Errors',
      pollIntervalMinutes: 5,
      lastPollAt: null,
      lastError: null,
      status: 'active',
    };

    await StorageManager.saveSubscription(sub);
    const subs = await StorageManager.getSubscriptions();
    expect(subs).toHaveLength(1);
    expect(subs[0].name).toBe('Prod Errors');
  });
});
```

**Step 2: Run to verify fail**

Run: `cd src/chrome-extension && npx vitest run --reporter=verbose`
Expected: FAIL — module not found

**Step 3: Implement storage utility**

```typescript
// src/chrome-extension/src/utils/storage.ts
import type { CapturedQuery, Subscription, ExtensionSettings } from '../shared/types';
import { DEFAULT_SETTINGS } from '../shared/types';

const KEYS = {
  settings: 'lj_settings',
  queries: 'lj_captured_queries',
  subscriptions: 'lj_subscriptions',
} as const;

export const StorageManager = {
  async getSettings(): Promise<ExtensionSettings> {
    const result = await chrome.storage.local.get([KEYS.settings]);
    return (result[KEYS.settings] as ExtensionSettings) ?? { ...DEFAULT_SETTINGS };
  },

  async saveSettings(settings: ExtensionSettings): Promise<void> {
    await chrome.storage.local.set({ [KEYS.settings]: settings });
  },

  async getCapturedQueries(): Promise<CapturedQuery[]> {
    const result = await chrome.storage.local.get([KEYS.queries]);
    return (result[KEYS.queries] as CapturedQuery[]) ?? [];
  },

  async addCapturedQuery(query: CapturedQuery): Promise<void> {
    const settings = await this.getSettings();
    const queries = await this.getCapturedQueries();

    // Deduplicate by query DSL content
    const existing = queries.findIndex(
      q => JSON.stringify(q.queryDsl) === JSON.stringify(query.queryDsl)
        && q.indexPattern === query.indexPattern
    );
    if (existing >= 0) {
      queries[existing] = { ...query, id: queries[existing].id };
    } else {
      queries.unshift(query);
    }

    // Trim to max
    const trimmed = queries.slice(0, settings.maxCapturedQueries);
    await chrome.storage.local.set({ [KEYS.queries]: trimmed });
  },

  async getSubscriptions(): Promise<Subscription[]> {
    const result = await chrome.storage.local.get([KEYS.subscriptions]);
    return (result[KEYS.subscriptions] as Subscription[]) ?? [];
  },

  async saveSubscription(subscription: Subscription): Promise<void> {
    const subs = await this.getSubscriptions();
    const idx = subs.findIndex(s => s.id === subscription.id);
    if (idx >= 0) {
      subs[idx] = subscription;
    } else {
      subs.push(subscription);
    }
    await chrome.storage.local.set({ [KEYS.subscriptions]: subs });
  },

  async removeSubscription(subscriptionId: string): Promise<void> {
    const subs = await this.getSubscriptions();
    await chrome.storage.local.set({
      [KEYS.subscriptions]: subs.filter(s => s.id !== subscriptionId),
    });
  },
};
```

**Step 4: Run tests**

Run: `cd src/chrome-extension && npx vitest run --reporter=verbose`
Expected: All tests pass

**Step 5: Commit**

```bash
git add src/chrome-extension/src/utils/storage.ts \
        src/chrome-extension/src/utils/__tests__/storage.test.ts
git commit -m "feat(extension): add chrome.storage wrapper for queries, subscriptions, settings"
```

---

## Phase C: Chrome Extension — Content Script & Service Worker

### Task 9: Create Kibana Discover content script (fetch interceptor)

**Files:**
- Create: `src/chrome-extension/src/content/kibana-interceptor.ts`

**Step 1: Implement the content script**

This script monkey-patches `window.fetch` to intercept Kibana's ES proxy calls.

```typescript
// src/chrome-extension/src/content/kibana-interceptor.ts

const KIBANA_SEARCH_PATTERNS = [
  '/internal/search/es',
  '/internal/bsearch',
  '/api/console/proxy',
  '/elasticsearch/',
  '/_search',
  '/_msearch',
];

function isKibanaSearchRequest(url: string): boolean {
  return KIBANA_SEARCH_PATTERNS.some(pattern => url.includes(pattern));
}

function patchFetch(): void {
  const originalFetch = window.fetch;

  window.fetch = async function (input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const method = init?.method ?? 'GET';

    if (method === 'POST' && isKibanaSearchRequest(url)) {
      try {
        const bodyText = typeof init?.body === 'string'
          ? init.body
          : init?.body instanceof ArrayBuffer
            ? new TextDecoder().decode(init.body)
            : null;

        if (bodyText) {
          // Kibana bsearch sends newline-delimited JSON
          const lines = bodyText.split('\n').filter(Boolean);
          for (const line of lines) {
            try {
              const parsed = JSON.parse(line);
              if (parsed.params?.body?.query || parsed.query) {
                chrome.runtime.sendMessage({
                  type: 'KIBANA_QUERY_CAPTURED',
                  payload: {
                    url,
                    method,
                    queryDsl: parsed.params?.body ?? parsed,
                    indexPattern: parsed.params?.index ?? extractIndexFromUrl(url),
                    kibanaUrl: window.location.origin,
                    capturedAt: new Date().toISOString(),
                  },
                });
                break; // Only capture the first meaningful query per request
              }
            } catch {
              // Skip non-JSON lines (e.g., NDJSON batch headers)
            }
          }
        }
      } catch {
        // Never break page functionality
      }
    }

    return originalFetch.call(this, input, init);
  };
}

function extractIndexFromUrl(url: string): string {
  const match = url.match(/\/([^/]+)\/_(?:search|msearch)/);
  return match ? match[1] : 'unknown';
}

// Run immediately at document_start
patchFetch();
```

**Step 2: Build to verify no TypeScript errors**

Run: `cd src/chrome-extension && npx tsc --noEmit`
Expected: No errors

**Step 3: Commit**

```bash
git add src/chrome-extension/src/content/kibana-interceptor.ts
git commit -m "feat(extension): add content script to intercept Kibana ES queries"
```

---

### Task 10: Create the service worker (background script)

**Files:**
- Create: `src/chrome-extension/src/background/service-worker.ts`

**Step 1: Implement service worker**

```typescript
// src/chrome-extension/src/background/service-worker.ts
import { StorageManager } from '../utils/storage';
import { summarizeQuery, extractIndexPattern } from '../shared/kibana-query-parser';
import type { CapturedQuery, Subscription, IngestEntry, IngestResponse } from '../shared/types';

// --- Message handling (from content script) ---

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message.type === 'KIBANA_QUERY_CAPTURED') {
    handleCapturedQuery(message.payload).then(() => sendResponse({ ok: true }));
    return true; // async response
  }

  if (message.type === 'GET_STATE') {
    getState().then(state => sendResponse(state));
    return true;
  }

  if (message.type === 'SUBSCRIBE') {
    handleSubscribe(message.payload).then(result => sendResponse(result));
    return true;
  }

  if (message.type === 'UNSUBSCRIBE') {
    handleUnsubscribe(message.payload.subscriptionId).then(() => sendResponse({ ok: true }));
    return true;
  }

  if (message.type === 'UPDATE_SETTINGS') {
    StorageManager.saveSettings(message.payload).then(() => sendResponse({ ok: true }));
    return true;
  }
});

async function handleCapturedQuery(payload: {
  url: string;
  method: string;
  queryDsl: Record<string, unknown>;
  indexPattern: string;
  kibanaUrl: string;
  capturedAt: string;
}): Promise<void> {
  const query: CapturedQuery = {
    id: crypto.randomUUID(),
    kibanaUrl: payload.kibanaUrl,
    proxyEndpoint: payload.url,
    method: payload.method,
    indexPattern: payload.indexPattern ?? extractIndexPattern(payload.url, payload.queryDsl),
    queryDsl: payload.queryDsl,
    summary: summarizeQuery(payload.queryDsl),
    capturedAt: payload.capturedAt,
  };
  await StorageManager.addCapturedQuery(query);
}

async function getState() {
  const [queries, subscriptions, settings] = await Promise.all([
    StorageManager.getCapturedQueries(),
    StorageManager.getSubscriptions(),
    StorageManager.getSettings(),
  ]);
  return { queries, subscriptions, settings };
}

// --- Subscription management ---

async function handleSubscribe(payload: {
  queryId: string;
  name: string;
  pollIntervalMinutes: number;
}): Promise<{ ok: boolean; error?: string; subscriptionId?: string }> {
  const queries = await StorageManager.getCapturedQueries();
  const query = queries.find(q => q.id === payload.queryId);
  if (!query) return { ok: false, error: 'Query not found' };

  const settings = await StorageManager.getSettings();

  // Create DataSource in Log Jammer
  try {
    const dsResponse = await fetch(`${settings.logJammerUrl}/api/datasources`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: payload.name,
        adapterType: 'KibanaProxy',
        connectionConfig: JSON.stringify({
          kibanaUrl: query.kibanaUrl,
          indexPattern: query.indexPattern,
          queryDsl: query.queryDsl,
          capturedAt: query.capturedAt,
        }),
        pollIntervalSeconds: payload.pollIntervalMinutes * 60,
        enabled: true,
      }),
    });

    if (!dsResponse.ok) {
      const error = await dsResponse.text();
      return { ok: false, error: `Failed to create DataSource: ${error}` };
    }

    const dataSource = await dsResponse.json() as { id: string };

    const subscription: Subscription = {
      id: crypto.randomUUID(),
      queryId: query.id,
      dataSourceId: dataSource.id,
      name: payload.name,
      pollIntervalMinutes: payload.pollIntervalMinutes,
      lastPollAt: null,
      lastError: null,
      status: 'active',
    };

    await StorageManager.saveSubscription(subscription);

    // Set up alarm
    chrome.alarms.create(`poll_${subscription.id}`, {
      periodInMinutes: payload.pollIntervalMinutes,
      delayInMinutes: 0, // Fire immediately, then on interval
    });

    return { ok: true, subscriptionId: subscription.id };
  } catch (err) {
    return { ok: false, error: `Network error: ${err instanceof Error ? err.message : String(err)}` };
  }
}

async function handleUnsubscribe(subscriptionId: string): Promise<void> {
  chrome.alarms.clear(`poll_${subscriptionId}`);
  await StorageManager.removeSubscription(subscriptionId);
}

// --- Alarm-driven polling ---

chrome.alarms.onAlarm.addListener(async (alarm) => {
  if (!alarm.name.startsWith('poll_')) return;

  const subscriptionId = alarm.name.replace('poll_', '');
  const subscriptions = await StorageManager.getSubscriptions();
  const subscription = subscriptions.find(s => s.id === subscriptionId);
  if (!subscription || subscription.status !== 'active') return;

  const queries = await StorageManager.getCapturedQueries();
  const query = queries.find(q => q.id === subscription.queryId);
  if (!query) return;

  await executePoll(subscription, query);
});

async function executePoll(subscription: Subscription, query: CapturedQuery): Promise<void> {
  const settings = await StorageManager.getSettings();

  try {
    // Adjust time range for incremental polling
    const adjustedQuery = adjustTimeRange(query.queryDsl, subscription.lastPollAt);

    // Execute query through Kibana's proxy
    const kibanaResponse = await fetch(`${query.kibanaUrl}${query.proxyEndpoint}`, {
      method: query.method,
      headers: { 'Content-Type': 'application/json', 'kbn-xsrf': 'true' },
      credentials: 'include',
      body: JSON.stringify(adjustedQuery),
    });

    if (kibanaResponse.status === 401 || kibanaResponse.status === 403) {
      subscription.status = 'paused';
      subscription.lastError = 'Kibana session expired. Visit Kibana to re-authenticate.';
      await StorageManager.saveSubscription(subscription);
      chrome.action.setBadgeText({ text: '!' });
      chrome.action.setBadgeBackgroundColor({ color: '#ff1744' });
      return;
    }

    if (!kibanaResponse.ok) {
      subscription.lastError = `Kibana returned ${kibanaResponse.status}`;
      await StorageManager.saveSubscription(subscription);
      return;
    }

    const data = await kibanaResponse.json() as Record<string, unknown>;
    const hits = extractHits(data);

    if (hits.length === 0) {
      subscription.lastPollAt = new Date().toISOString();
      subscription.lastError = null;
      await StorageManager.saveSubscription(subscription);
      return;
    }

    // Push to Log Jammer
    const entries: IngestEntry[] = hits.map(hit => ({
      timestamp: (hit._source as Record<string, unknown>)?.['@timestamp'] as string
        ?? new Date().toISOString(),
      fields: hit._source as Record<string, unknown> ?? {},
    }));

    const ingestResponse = await fetch(
      `${settings.logJammerUrl}/api/ingest/${subscription.dataSourceId}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ entries }),
      }
    );

    if (!ingestResponse.ok) {
      subscription.lastError = `Log Jammer returned ${ingestResponse.status}`;
    } else {
      const result = await ingestResponse.json() as IngestResponse;
      subscription.lastError = null;
      console.log(`[LogJammer] Pushed ${result.accepted} new, ${result.duplicates} duplicate entries`);
    }

    subscription.lastPollAt = new Date().toISOString();
    await StorageManager.saveSubscription(subscription);

  } catch (err) {
    subscription.lastError = err instanceof Error ? err.message : String(err);
    await StorageManager.saveSubscription(subscription);
  }
}

function adjustTimeRange(
  queryDsl: Record<string, unknown>,
  lastPollAt: string | null
): Record<string, unknown> {
  if (!lastPollAt) return queryDsl;

  // Deep clone
  const adjusted = JSON.parse(JSON.stringify(queryDsl)) as Record<string, unknown>;

  // Try to find and update range filter on @timestamp
  const query = adjusted.query as Record<string, unknown> | undefined;
  if (!query) return adjusted;

  const bool = query.bool as Record<string, unknown> | undefined;
  if (!bool?.filter || !Array.isArray(bool.filter)) return adjusted;

  for (const clause of bool.filter as Record<string, unknown>[]) {
    if ('range' in clause) {
      const range = clause.range as Record<string, Record<string, unknown>>;
      if ('@timestamp' in range) {
        range['@timestamp'].gte = lastPollAt;
        range['@timestamp'].lte = 'now';
        return adjusted;
      }
    }
  }

  // No existing range found — add one
  (bool.filter as Record<string, unknown>[]).push({
    range: { '@timestamp': { gte: lastPollAt, lte: 'now' } }
  });
  return adjusted;
}

function extractHits(data: Record<string, unknown>): Array<Record<string, unknown>> {
  // Standard ES response
  if (data.hits && typeof data.hits === 'object') {
    const hits = data.hits as Record<string, unknown>;
    if (Array.isArray(hits.hits)) return hits.hits as Array<Record<string, unknown>>;
  }

  // Kibana bsearch wraps in rawResponse
  if (data.rawResponse && typeof data.rawResponse === 'object') {
    return extractHits(data.rawResponse as Record<string, unknown>);
  }

  // Kibana bsearch array response
  if (Array.isArray(data)) {
    for (const item of data) {
      if (typeof item === 'object' && item !== null) {
        const hits = extractHits(item as Record<string, unknown>);
        if (hits.length > 0) return hits;
      }
    }
  }

  return [];
}

// --- Startup: restore alarms for active subscriptions ---

async function restoreAlarms(): Promise<void> {
  const subscriptions = await StorageManager.getSubscriptions();
  for (const sub of subscriptions) {
    if (sub.status === 'active') {
      chrome.alarms.create(`poll_${sub.id}`, {
        periodInMinutes: sub.pollIntervalMinutes,
        delayInMinutes: 1,
      });
    }
  }
}

restoreAlarms();
```

**Step 2: Build**

Run: `cd src/chrome-extension && npx tsc --noEmit`
Expected: No TypeScript errors

**Step 3: Commit**

```bash
git add src/chrome-extension/src/background/service-worker.ts
git commit -m "feat(extension): add service worker with alarm-based polling and LJ push"
```

---

## Phase D: Chrome Extension — Popup UI

### Task 11: Create the MUI theme and popup shell

**Files:**
- Create: `src/chrome-extension/src/popup/popup.html`
- Create: `src/chrome-extension/src/popup/popup.tsx`
- Create: `src/chrome-extension/src/popup/theme.ts`

**Step 1: Create popup.html**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Log Jammer Kibana Bridge</title>
  <style>
    body { margin: 0; width: 420px; min-height: 500px; }
  </style>
</head>
<body>
  <div id="root"></div>
  <script type="module" src="./popup.tsx"></script>
</body>
</html>
```

**Step 2: Create theme** (adapted from Log Jammer frontend theme)

```typescript
// src/chrome-extension/src/popup/theme.ts
import { createTheme } from '@mui/material/styles';

const theme = createTheme({
  palette: {
    mode: 'dark',
    background: { default: '#0a0e14', paper: '#0d1117' },
    primary: { main: '#00e5ff' },
    secondary: { main: '#ffb300' },
    error: { main: '#ff1744' },
    warning: { main: '#ff9100' },
    success: { main: '#00e676' },
  },
  typography: {
    fontFamily: "'IBM Plex Sans Condensed', 'Inter', 'Roboto', sans-serif",
    fontSize: 13,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: { backgroundColor: '#0a0e14' },
      },
    },
  },
});

export default theme;
```

**Step 3: Create popup entry point**

```tsx
// src/chrome-extension/src/popup/popup.tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import theme from './theme';
import App from './App';

const root = document.getElementById('root')!;
createRoot(root).render(
  <StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <App />
    </ThemeProvider>
  </StrictMode>
);
```

**Step 4: Create App component (tab layout)**

```tsx
// src/chrome-extension/src/popup/App.tsx
import { useState, useEffect } from 'react';
import Box from '@mui/material/Box';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Typography from '@mui/material/Typography';
import RecentQueries from './components/RecentQueries';
import ActiveSubscriptions from './components/ActiveSubscriptions';
import Settings from './components/Settings';
import type { CapturedQuery, Subscription, ExtensionSettings } from '../shared/types';

export default function App() {
  const [tab, setTab] = useState(0);
  const [queries, setQueries] = useState<CapturedQuery[]>([]);
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
  const [settings, setSettings] = useState<ExtensionSettings | null>(null);

  const refreshState = () => {
    chrome.runtime.sendMessage({ type: 'GET_STATE' }, (response) => {
      if (response) {
        setQueries(response.queries ?? []);
        setSubscriptions(response.subscriptions ?? []);
        setSettings(response.settings ?? null);
      }
    });
  };

  useEffect(() => { refreshState(); }, []);

  return (
    <Box sx={{ width: '100%' }}>
      <Box sx={{ px: 2, pt: 1.5, pb: 0.5, display: 'flex', alignItems: 'center', gap: 1 }}>
        <Typography variant="subtitle1" fontWeight={700} color="primary">
          Log Jammer
        </Typography>
        <Typography variant="caption" color="text.secondary">
          Kibana Bridge
        </Typography>
      </Box>
      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="fullWidth" sx={{ minHeight: 36 }}>
        <Tab label={`Queries (${queries.length})`} sx={{ minHeight: 36, py: 0 }} />
        <Tab label={`Active (${subscriptions.length})`} sx={{ minHeight: 36, py: 0 }} />
        <Tab label="Settings" sx={{ minHeight: 36, py: 0 }} />
      </Tabs>
      <Box sx={{ p: 1.5 }}>
        {tab === 0 && <RecentQueries queries={queries} onSubscribe={refreshState} />}
        {tab === 1 && <ActiveSubscriptions subscriptions={subscriptions} onUpdate={refreshState} />}
        {tab === 2 && settings && <Settings settings={settings} onSave={refreshState} />}
      </Box>
    </Box>
  );
}
```

**Step 5: Commit (placeholder components in next task)**

```bash
git add src/chrome-extension/src/popup/
git commit -m "feat(extension): add popup shell with MUI theme and tab layout"
```

---

### Task 12: Create popup components — RecentQueries

**Files:**
- Create: `src/chrome-extension/src/popup/components/RecentQueries.tsx`

**Step 1: Implement**

```tsx
// src/chrome-extension/src/popup/components/RecentQueries.tsx
import { useState } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import type { CapturedQuery } from '../../shared/types';

interface Props {
  queries: CapturedQuery[];
  onSubscribe: () => void;
}

export default function RecentQueries({ queries, onSubscribe }: Props) {
  const [subscribeTarget, setSubscribeTarget] = useState<CapturedQuery | null>(null);
  const [name, setName] = useState('');
  const [interval, setInterval] = useState('5');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubscribe = async () => {
    if (!subscribeTarget) return;
    setLoading(true);
    setError(null);

    chrome.runtime.sendMessage(
      {
        type: 'SUBSCRIBE',
        payload: {
          queryId: subscribeTarget.id,
          name,
          pollIntervalMinutes: parseInt(interval, 10),
        },
      },
      (response) => {
        setLoading(false);
        if (response?.ok) {
          setSubscribeTarget(null);
          onSubscribe();
        } else {
          setError(response?.error ?? 'Unknown error');
        }
      }
    );
  };

  if (queries.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" textAlign="center" py={4}>
        No queries captured yet. Search in Kibana Discover to see queries here.
      </Typography>
    );
  }

  return (
    <>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        {queries.map((q) => (
          <Card key={q.id} variant="outlined" sx={{ bgcolor: 'background.paper' }}>
            <CardContent sx={{ py: 1, px: 1.5, '&:last-child': { pb: 1 } }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Chip label={q.indexPattern} size="small" color="primary" variant="outlined" sx={{ mb: 0.5 }} />
                  <Typography variant="body2" sx={{ fontFamily: 'monospace', fontSize: 11, wordBreak: 'break-all' }}>
                    {q.summary}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {new Date(q.capturedAt).toLocaleTimeString()}
                  </Typography>
                </Box>
                <Button
                  size="small"
                  variant="contained"
                  onClick={() => {
                    setSubscribeTarget(q);
                    setName(`${q.indexPattern} — ${q.summary}`.slice(0, 60));
                    setInterval('5');
                  }}
                  sx={{ ml: 1, whiteSpace: 'nowrap' }}
                >
                  Subscribe
                </Button>
              </Box>
            </CardContent>
          </Card>
        ))}
      </Box>

      <Dialog open={!!subscribeTarget} onClose={() => setSubscribeTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Subscribe to Query</DialogTitle>
        <DialogContent>
          <TextField
            label="Name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            fullWidth
            margin="normal"
            size="small"
          />
          <TextField
            label="Poll interval (minutes)"
            type="number"
            value={interval}
            onChange={(e) => setInterval(e.target.value)}
            fullWidth
            margin="normal"
            size="small"
            slotProps={{ htmlInput: { min: 1, max: 1440 } }}
          />
          {error && (
            <Typography color="error" variant="body2" sx={{ mt: 1 }}>
              {error}
            </Typography>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSubscribeTarget(null)}>Cancel</Button>
          <Button
            onClick={handleSubscribe}
            variant="contained"
            disabled={loading || !name.trim()}
          >
            {loading ? 'Creating...' : 'Subscribe'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
```

**Step 2: Commit**

```bash
git add src/chrome-extension/src/popup/components/RecentQueries.tsx
git commit -m "feat(extension): add RecentQueries popup component with subscribe dialog"
```

---

### Task 13: Create popup components — ActiveSubscriptions

**Files:**
- Create: `src/chrome-extension/src/popup/components/ActiveSubscriptions.tsx`

**Step 1: Implement**

```tsx
// src/chrome-extension/src/popup/components/ActiveSubscriptions.tsx
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import Chip from '@mui/material/Chip';
import DeleteIcon from '@mui/icons-material/Delete';
import type { Subscription } from '../../shared/types';

interface Props {
  subscriptions: Subscription[];
  onUpdate: () => void;
}

const statusColors: Record<Subscription['status'], 'success' | 'warning' | 'error'> = {
  active: 'success',
  paused: 'warning',
  error: 'error',
};

export default function ActiveSubscriptions({ subscriptions, onUpdate }: Props) {
  const handleDelete = (id: string) => {
    chrome.runtime.sendMessage({ type: 'UNSUBSCRIBE', payload: { subscriptionId: id } }, () => {
      onUpdate();
    });
  };

  if (subscriptions.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" textAlign="center" py={4}>
        No active subscriptions. Subscribe to a captured query to start feeding data.
      </Typography>
    );
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
      {subscriptions.map((sub) => (
        <Card key={sub.id} variant="outlined" sx={{ bgcolor: 'background.paper' }}>
          <CardContent sx={{ py: 1, px: 1.5, '&:last-child': { pb: 1 } }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.5 }}>
                  <Typography variant="body2" fontWeight={600} noWrap>
                    {sub.name}
                  </Typography>
                  <Chip label={sub.status} size="small" color={statusColors[sub.status]} />
                </Box>
                <Typography variant="caption" color="text.secondary" display="block">
                  Every {sub.pollIntervalMinutes} min
                  {sub.lastPollAt && ` · Last: ${new Date(sub.lastPollAt).toLocaleTimeString()}`}
                </Typography>
                {sub.lastError && (
                  <Typography variant="caption" color="error" display="block" sx={{ mt: 0.5 }}>
                    {sub.lastError}
                  </Typography>
                )}
              </Box>
              <IconButton size="small" onClick={() => handleDelete(sub.id)} color="error">
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Box>
          </CardContent>
        </Card>
      ))}
    </Box>
  );
}
```

**Step 2: Commit**

```bash
git add src/chrome-extension/src/popup/components/ActiveSubscriptions.tsx
git commit -m "feat(extension): add ActiveSubscriptions popup component"
```

---

### Task 14: Create popup components — Settings

**Files:**
- Create: `src/chrome-extension/src/popup/components/Settings.tsx`

**Step 1: Implement**

```tsx
// src/chrome-extension/src/popup/components/Settings.tsx
import { useState } from 'react';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import type { ExtensionSettings } from '../../shared/types';

interface Props {
  settings: ExtensionSettings;
  onSave: () => void;
}

export default function Settings({ settings, onSave }: Props) {
  const [url, setUrl] = useState(settings.logJammerUrl);
  const [maxQueries, setMaxQueries] = useState(String(settings.maxCapturedQueries));
  const [saved, setSaved] = useState(false);

  const handleSave = () => {
    chrome.runtime.sendMessage(
      {
        type: 'UPDATE_SETTINGS',
        payload: {
          logJammerUrl: url.replace(/\/+$/, ''), // trim trailing slash
          maxCapturedQueries: parseInt(maxQueries, 10) || 50,
        },
      },
      () => {
        setSaved(true);
        setTimeout(() => setSaved(false), 2000);
        onSave();
      }
    );
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <TextField
        label="Log Jammer URL"
        value={url}
        onChange={(e) => setUrl(e.target.value)}
        size="small"
        fullWidth
        placeholder="http://localhost:5050"
        helperText="The URL of your Log Jammer instance"
      />
      <TextField
        label="Max captured queries"
        type="number"
        value={maxQueries}
        onChange={(e) => setMaxQueries(e.target.value)}
        size="small"
        fullWidth
        slotProps={{ htmlInput: { min: 10, max: 200 } }}
      />
      <Button variant="contained" onClick={handleSave}>
        Save Settings
      </Button>
      {saved && <Alert severity="success" sx={{ py: 0 }}>Settings saved</Alert>}

      <Box sx={{ mt: 2, pt: 2, borderTop: 1, borderColor: 'divider' }}>
        <Typography variant="caption" color="text.secondary">
          Log Jammer Kibana Bridge v0.1.0
        </Typography>
      </Box>
    </Box>
  );
}
```

**Step 2: Build the full extension**

Run: `cd src/chrome-extension && npm run build`
Expected: Build succeeds, dist/ directory created with manifest.json, service-worker.js, content-script.js, popup/

**Step 3: Commit**

```bash
git add src/chrome-extension/src/popup/components/Settings.tsx
git commit -m "feat(extension): add Settings popup component"
```

---

### Task 15: Finalize build and add .gitignore

**Files:**
- Modify: `src/chrome-extension/manifest.json` (update paths to match build output)
- Create: `src/chrome-extension/.gitignore`
- Modify: `.gitignore` (root)

**Step 1: Copy manifest to dist during build**

Update `vite.config.ts` to copy `manifest.json` and `icons/` to `dist/`:

```typescript
// Add to vite.config.ts build.rollupOptions:
// Also add publicDir: false and a manual copy in a plugin, OR
// use Vite's public directory feature:
```

Simpler approach: add a `postbuild` script to package.json:

```json
"scripts": {
  "dev": "vite build --watch",
  "build": "tsc -b && vite build && cp manifest.json dist/ && cp -r icons dist/",
  "test": "vitest",
  "test:run": "vitest run"
}
```

**Step 2: Create .gitignore**

```
# src/chrome-extension/.gitignore
node_modules/
dist/
```

**Step 3: Run the full build and verify dist/**

Run: `cd src/chrome-extension && npm run build`
Expected: `dist/` contains: `manifest.json`, `service-worker.js`, `content-script.js`, `src/popup/popup.html`, `icons/`

**Step 4: Verify extension loads in Chrome**

Manual test:
1. Open `chrome://extensions/`
2. Enable Developer mode
3. Click "Load unpacked"
4. Select `src/chrome-extension/dist/`
5. Extension should appear without errors

**Step 5: Commit**

```bash
git add src/chrome-extension/.gitignore src/chrome-extension/package.json src/chrome-extension/vite.config.ts
git commit -m "chore(extension): finalize build config and gitignore"
```

---

### Task 16: Run all tests and verify everything builds

**Step 1: Backend tests**

Run: `dotnet test src/LogJammer.slnx`
Expected: All tests pass

**Step 2: Frontend tests**

Run: `cd src/frontend && npm test -- --run`
Expected: All tests pass

**Step 3: Extension tests**

Run: `cd src/chrome-extension && npm test -- --run`
Expected: All tests pass (query parser + storage tests)

**Step 4: Final commit with any fixes**

```bash
git add -A
git commit -m "chore: verify all tests pass for Kibana bridge feature"
```

---

## Summary

| Phase | Tasks | Description |
|-------|-------|-------------|
| A | 1-5 | Backend: KibanaProxy enum, extract ingestion pipeline, ingest endpoint, specs update |
| B | 6-8 | Extension: project scaffold, query parser, storage wrapper |
| C | 9-10 | Extension: content script (fetch interceptor), service worker (polling + push) |
| D | 11-16 | Extension: popup UI (queries, subscriptions, settings), build config, final verification |
