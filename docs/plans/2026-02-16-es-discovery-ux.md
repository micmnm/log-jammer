# Elasticsearch Discovery UX Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add inline Elasticsearch index/alias discovery and schema browsing to the DataSource dialog, so users don't have to type config values blind.

**Architecture:** Two new POST endpoints on DataSourcesController accept raw connection config JSON (no saved data source needed) and proxy ES cluster APIs. The frontend adds discover/schema buttons to the Elasticsearch section of the existing DataSourceDialog.

**Tech Stack:** C# / ASP.NET Core 10, Elastic.Clients.Elasticsearch, React 19 / MUI v7 / TanStack Query v5

**Status:** All tasks NOT STARTED. Plan updated 2026-02-17 to align with current codebase (post-detect-endpoint, LogFile detect UI, deletion-impact, NOC redesign, etc.).

**Codebase note:** The existing `Detect` endpoint (`POST /api/datasources/detect`) and its LogFile UI in `DataSourceDialog.tsx` provide a close architectural precedent for these discovery endpoints. Follow the same patterns.

**Connection config note:** `ElasticsearchConnectionConfig` uses a nested `Auth` object (`{ url, indexPattern, auth: { type: "basic", username, password } }`). The frontend's `buildConnectionConfig()` currently builds a flat `{ url, indexPattern, username, password }` — this is a pre-existing mismatch. For discovery, build the config JSON to match what `ElasticsearchAdapter` actually deserializes (nested auth). Consider fixing `buildConnectionConfig()` for the ES case to use the correct nested format while you're in there.

---

### Task 1: Add Discovery DTOs

**Files:**
- Modify: `src/LogJammer.Api/Dtos/DataSourceDtos.cs`

**Step 1: Add the new request/response DTOs to the existing file**

Add at the end of `src/LogJammer.Api/Dtos/DataSourceDtos.cs` (after `DeletionImpactResponse`):

```csharp
public record DiscoverIndicesRequest
{
    [Required]
    public required string ConnectionConfig { get; init; }

    public bool ShowConcreteIndices { get; init; } = false;
}

public record DiscoverSchemaRequest
{
    [Required]
    public required string ConnectionConfig { get; init; }
}

public record DiscoverIndicesResponse
{
    public required IReadOnlyList<AliasInfo> Aliases { get; init; }
    public required IReadOnlyList<DataStreamInfo> DataStreams { get; init; }
    public IReadOnlyList<string> ConcreteIndices { get; init; } = [];
}

public record AliasInfo
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> Indices { get; init; }
}

public record DataStreamInfo
{
    public required string Name { get; init; }
    public int BackingIndices { get; init; }
}
```

**Step 2: Verify it compiles**

Run: `dotnet build src/LogJammer.Api/LogJammer.Api.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/LogJammer.Api/Dtos/DataSourceDtos.cs
git commit -m "feat: add discovery DTOs for ES index/schema browsing"
```

---

### Task 2: Add Discovery Methods to ElasticsearchAdapter

**Files:**
- Modify: `src/LogJammer.Infrastructure/Adapters/Elasticsearch/ElasticsearchAdapter.cs`

**Context:** The adapter currently has `TestConnectionAsync`, `PollErrorsAsync`, `GetSampleRecordsAsync`, `GetSchemaAsync`, plus private helpers `ParseHits` and `FlattenProperties`. It uses `_client` (ElasticsearchClient) and `_config` (ElasticsearchConnectionConfig). The existing `using` directives already include `Elastic.Clients.Elasticsearch.IndexManagement`.

**Step 1: Add `DiscoverIndicesAsync` method**

Add to `ElasticsearchAdapter` class, after `GetSchemaAsync` (before `ParseHits`):

```csharp
public async Task<(IReadOnlyList<(string Alias, IReadOnlyList<string> Indices)> Aliases,
    IReadOnlyList<(string Name, int BackingIndices)> DataStreams,
    IReadOnlyList<string> ConcreteIndices)> DiscoverIndicesAsync(
    bool includeConcreteIndices, CancellationToken cancellationToken = default)
{
    // Get aliases
    var aliases = new List<(string Alias, IReadOnlyList<string> Indices)>();
    try
    {
        var aliasResponse = await _client.Indices.GetAliasAsync(new GetAliasRequest(), cancellationToken);
        if (aliasResponse.IsValidResponse && aliasResponse.Indices is not null)
        {
            var aliasMap = new Dictionary<string, List<string>>();
            foreach (var (indexName, indexAliases) in aliasResponse.Indices)
            {
                if (indexAliases.Aliases is null) continue;
                foreach (var (aliasName, _) in indexAliases.Aliases)
                {
                    if (!aliasMap.ContainsKey(aliasName.ToString()))
                        aliasMap[aliasName.ToString()] = [];
                    aliasMap[aliasName.ToString()].Add(indexName.ToString());
                }
            }
            aliases = aliasMap.Select(kvp =>
                ((string Alias, IReadOnlyList<string> Indices))(kvp.Key, kvp.Value.AsReadOnly())).ToList();
        }
    }
    catch { /* alias discovery is best-effort */ }

    // Get data streams
    var dataStreams = new List<(string Name, int BackingIndices)>();
    try
    {
        var dsResponse = await _client.Indices.GetDataStreamAsync(
            new GetDataStreamRequest(), cancellationToken);
        if (dsResponse.IsValidResponse && dsResponse.DataStreams is not null)
        {
            foreach (var ds in dsResponse.DataStreams)
            {
                dataStreams.Add((ds.Name, ds.Indices?.Count ?? 0));
            }
        }
    }
    catch { /* data stream discovery is best-effort */ }

    // Get concrete indices (optional)
    var concreteIndices = new List<string>();
    if (includeConcreteIndices)
    {
        try
        {
            var catResponse = await _client.Cat.IndicesAsync(
                new Elastic.Clients.Elasticsearch.Cat.CatIndicesRequest(), cancellationToken);
            if (catResponse.IsValidResponse && catResponse.Indices is not null)
            {
                foreach (var idx in catResponse.Indices)
                {
                    if (idx.Index is not null)
                        concreteIndices.Add(idx.Index);
                }
            }
        }
        catch { /* index discovery is best-effort */ }
    }

    return (aliases.AsReadOnly(), dataStreams.AsReadOnly(), concreteIndices.AsReadOnly());
}
```

**Step 2: Verify it compiles**

Run: `dotnet build src/LogJammer.Infrastructure/LogJammer.Infrastructure.csproj`
Expected: Build succeeded. Note: The exact Elasticsearch client API shape for `GetAlias`, `GetDataStream`, and `Cat.Indices` may need adjustment based on the `Elastic.Clients.Elasticsearch` 9.x API. Check the client types if the build fails and adjust accordingly. `GetDataStreamRequest` is in the `IndexManagement` namespace which is already imported.

**Step 3: Commit**

```bash
git add src/LogJammer.Infrastructure/Adapters/Elasticsearch/ElasticsearchAdapter.cs
git commit -m "feat: add DiscoverIndicesAsync to ElasticsearchAdapter"
```

---

### Task 3: Add Discovery Methods to DataSourceService

**Files:**
- Modify: `src/LogJammer.Api/Services/IDataSourceService.cs`
- Modify: `src/LogJammer.Api/Services/DataSourceService.cs`

**Context:** `IDataSourceService` already has `using LogJammer.Api.Dtos;`. `DataSourceService` uses primary constructor syntax: `DataSourceService(IDataSourceRepository repository, IDataSourceAdapterFactory adapterFactory) : IDataSourceService`. The factory creates adapters via `adapterFactory.CreateAdapter(AdapterType, connectionConfig)`.

**Step 1: Add interface methods**

Add to `IDataSourceService` (after `GetSampleRecordsAsync`):

```csharp
Task<DiscoverIndicesResponse> DiscoverIndicesAsync(DiscoverIndicesRequest request, CancellationToken cancellationToken = default);
Task<SchemaResponse> DiscoverSchemaAsync(DiscoverSchemaRequest request, CancellationToken cancellationToken = default);
```

**Step 2: Implement in DataSourceService**

Add to `DataSourceService` (before `MapToResponse`):

```csharp
public async Task<DiscoverIndicesResponse> DiscoverIndicesAsync(DiscoverIndicesRequest request, CancellationToken cancellationToken = default)
{
    var adapter = (ElasticsearchAdapter)adapterFactory.CreateAdapter(
        AdapterType.Elasticsearch, request.ConnectionConfig);

    var (aliases, dataStreams, concreteIndices) = await adapter.DiscoverIndicesAsync(
        request.ShowConcreteIndices, cancellationToken);

    return new DiscoverIndicesResponse
    {
        Aliases = aliases.Select(a => new AliasInfo
        {
            Name = a.Alias,
            Indices = a.Indices
        }).ToList(),
        DataStreams = dataStreams.Select(ds => new DataStreamInfo
        {
            Name = ds.Name,
            BackingIndices = ds.BackingIndices
        }).ToList(),
        ConcreteIndices = concreteIndices
    };
}

public async Task<SchemaResponse> DiscoverSchemaAsync(DiscoverSchemaRequest request, CancellationToken cancellationToken = default)
{
    var adapter = adapterFactory.CreateAdapter(AdapterType.Elasticsearch, request.ConnectionConfig);
    var fields = await adapter.GetSchemaAsync(cancellationToken);

    return new SchemaResponse
    {
        Fields = fields.Select(f => new FieldDefinitionDto
        {
            Name = f.Name,
            Type = f.Type,
            IsNullable = f.IsNullable
        }).ToList()
    };
}
```

Add the cast import at the top of `DataSourceService.cs`:
```csharp
using LogJammer.Infrastructure.Adapters.Elasticsearch;
```

**Step 3: Verify it compiles**

Run: `dotnet build src/LogJammer.Api/LogJammer.Api.csproj`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/LogJammer.Api/Services/IDataSourceService.cs src/LogJammer.Api/Services/DataSourceService.cs
git commit -m "feat: add DiscoverIndices/DiscoverSchema to DataSourceService"
```

---

### Task 4: Add Discovery Endpoints to Controller

**Files:**
- Modify: `src/LogJammer.Api/Controllers/DataSourcesController.cs`

**Context:** The controller uses primary constructor: `DataSourcesController(IDataSourceService dataSourceService, ILogFileDetectService logFileDetectService) : ControllerBase`. It already imports `LogJammer.Api.Dtos`. The existing `Detect` endpoint provides the error handling pattern to follow (specific exception types, `Problem()` for errors).

**Step 1: Add the two new endpoints**

Add to `DataSourcesController` (before the `Detect` method):

```csharp
[HttpPost("discover/indices")]
public async Task<ActionResult<DiscoverIndicesResponse>> DiscoverIndices(
    [FromBody] DiscoverIndicesRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var result = await dataSourceService.DiscoverIndicesAsync(request, cancellationToken);
        return Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Problem(detail: ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        return Problem(detail: $"Discovery failed: {ex.Message}", statusCode: 502);
    }
}

[HttpPost("discover/schema")]
public async Task<ActionResult<SchemaResponse>> DiscoverSchema(
    [FromBody] DiscoverSchemaRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var result = await dataSourceService.DiscoverSchemaAsync(request, cancellationToken);
        return Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Problem(detail: ex.Message, statusCode: 400);
    }
    catch (Exception ex)
    {
        return Problem(detail: $"Schema discovery failed: {ex.Message}", statusCode: 502);
    }
}
```

**Step 2: Verify it compiles**

Run: `dotnet build src/LogJammer.Api/LogJammer.Api.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/LogJammer.Api/Controllers/DataSourcesController.cs
git commit -m "feat: add discover/indices and discover/schema endpoints"
```

---

### Task 5: Add Backend Tests for Discovery Endpoints

**Files:**
- Modify: `src/LogJammer.Tests/Integration/Api/DataSourcesControllerTests.cs`

**Context:** Tests use `TestWebApplicationFactory` which mocks `IDataSourceService` via NSubstitute. The test class has `_factory`, `_client`, `_service`, and `_jsonOptions` fields. Existing tests like `GetSchema_WithLogFileSource_ReturnsFields` provide the pattern.

**Step 1: Write the tests**

Add to `DataSourcesControllerTests`:

```csharp
[Fact]
public async Task DiscoverIndices_ReturnsAliasesAndDataStreams()
{
    _service.DiscoverIndicesAsync(Arg.Any<DiscoverIndicesRequest>(), Arg.Any<CancellationToken>())
        .Returns(new DiscoverIndicesResponse
        {
            Aliases = [new AliasInfo { Name = "app-logs", Indices = ["app-logs-2024.01"] }],
            DataStreams = [new DataStreamInfo { Name = "logs-nginx", BackingIndices = 3 }],
            ConcreteIndices = []
        });

    var request = new DiscoverIndicesRequest { ConnectionConfig = "{\"url\":\"http://localhost:9200\",\"indexPattern\":\"*\"}" };
    var response = await _client.PostAsJsonAsync("/api/datasources/discover/indices", request);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<DiscoverIndicesResponse>(_jsonOptions);
    body!.Aliases.Should().HaveCount(1);
    body.Aliases[0].Name.Should().Be("app-logs");
    body.DataStreams.Should().HaveCount(1);
    body.DataStreams[0].Name.Should().Be("logs-nginx");
}

[Fact]
public async Task DiscoverSchema_ReturnsFields()
{
    _service.DiscoverSchemaAsync(Arg.Any<DiscoverSchemaRequest>(), Arg.Any<CancellationToken>())
        .Returns(new SchemaResponse
        {
            Fields = [new FieldDefinitionDto { Name = "@timestamp", Type = "date", IsNullable = false },
                      new FieldDefinitionDto { Name = "message", Type = "text", IsNullable = true }]
        });

    var request = new DiscoverSchemaRequest { ConnectionConfig = "{\"url\":\"http://localhost:9200\",\"indexPattern\":\"app-logs\"}" };
    var response = await _client.PostAsJsonAsync("/api/datasources/discover/schema", request);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<SchemaResponse>(_jsonOptions);
    body!.Fields.Should().HaveCount(2);
    body.Fields.Should().Contain(f => f.Name == "message");
}
```

**Step 2: Run the tests**

Run: `dotnet test src/LogJammer.Tests/LogJammer.Tests.csproj --filter "FullyQualifiedName~DataSourcesControllerTests"`
Expected: All tests pass (including the two new ones)

**Step 3: Commit**

```bash
git add src/LogJammer.Tests/Integration/Api/DataSourcesControllerTests.cs
git commit -m "test: add tests for discover/indices and discover/schema endpoints"
```

---

### Task 6: Add Frontend Types and API Hooks

**Files:**
- Modify: `src/frontend/src/api/types.ts`
- Modify: `src/frontend/src/api/hooks/useDataSources.ts`

**Context:** `types.ts` currently ends with `DetectResponse` (line 269). `useDataSources.ts` exports `useDetectLogFile` as the last hook (line 93) and already imports `useMutation` from `@tanstack/react-query` and `api` from `../client`.

**Step 1: Add TypeScript types**

Add at the end of `src/frontend/src/api/types.ts`:

```typescript
export interface DiscoverIndicesRequest {
  connectionConfig: string;
  showConcreteIndices?: boolean;
}

export interface DiscoverSchemaRequest {
  connectionConfig: string;
}

export interface AliasInfo {
  name: string;
  indices: string[];
}

export interface DataStreamInfo {
  name: string;
  backingIndices: number;
}

export interface DiscoverIndicesResponse {
  aliases: AliasInfo[];
  dataStreams: DataStreamInfo[];
  concreteIndices: string[];
}
```

**Step 2: Add mutation hooks**

Add the new type imports to the existing import block in `src/frontend/src/api/hooks/useDataSources.ts`:

```typescript
import type {
  DataSourceResponse,
  CreateDataSourceRequest,
  UpdateDataSourceRequest,
  ConnectionTestResponse,
  SchemaResponse,
  SampleRecordsResponse,
  DetectResponse,
  DeletionImpactResponse,
  DiscoverIndicesRequest,
  DiscoverIndicesResponse,
  DiscoverSchemaRequest,
} from '../types';
```

Then add at the end of the file:

```typescript
export function useDiscoverIndices() {
  return useMutation({
    mutationFn: (request: DiscoverIndicesRequest) =>
      api.post<DiscoverIndicesResponse>('/datasources/discover/indices', request),
  });
}

export function useDiscoverSchema() {
  return useMutation({
    mutationFn: (request: DiscoverSchemaRequest) =>
      api.post<SchemaResponse>('/datasources/discover/schema', request),
  });
}
```

**Step 3: Verify frontend compiles**

Run: `cd src/frontend && npx tsc --noEmit`
Expected: No errors

**Step 4: Commit**

```bash
git add src/frontend/src/api/types.ts src/frontend/src/api/hooks/useDataSources.ts
git commit -m "feat: add frontend types and hooks for ES discovery"
```

---

### Task 7: Update DataSourceDialog with Discovery UI

**Files:**
- Modify: `src/frontend/src/components/DataSourceDialog.tsx`

**Context:** The dialog already imports `Chip`, `CircularProgress`, `Alert`, `Box`, `Typography` from MUI and has a `Detect` button pattern for LogFile (line 297-304) that serves as a close template. The `buildConnectionConfig()` method on line 160 builds ES config. `ElasticsearchConfig` interface on line 30 uses flat `{ url, indexPattern, username, password }`.

**IMPORTANT:** The backend `ElasticsearchConnectionConfig` expects `{ url, indexPattern, auth: { type: "basic", username, password } }` (nested auth). Fix the `ElasticsearchConfig` interface and `buildConnectionConfig()` to match the backend format, OR build a separate `buildDiscoveryConfig()` that produces the correct format for the discovery endpoints.

**Step 1: Fix ElasticsearchConfig and buildConnectionConfig**

Update the `ElasticsearchConfig` interface and `buildConnectionConfig()` to produce config JSON that matches `ElasticsearchConnectionConfig` on the backend:

```typescript
interface ElasticsearchConfig {
  url: string;
  indexPattern: string;
  auth?: {
    type: string;
    username?: string;
    password?: string;
  };
}
```

Update `buildConnectionConfig()`:
```typescript
if (adapterType === 'Elasticsearch') {
  const config: ElasticsearchConfig = { url: esUrl, indexPattern: esIndexPattern };
  if (esUsername || esPassword) {
    config.auth = { type: 'basic', username: esUsername, password: esPassword };
  }
  return JSON.stringify(config);
}
```

**Step 2: Add discovery imports and state**

Add to imports:
```typescript
import { useDiscoverIndices, useDiscoverSchema } from '../api/hooks/useDataSources';
import type { DiscoverIndicesResponse } from '../api/types';
```

Add MUI imports not already present: `List`, `ListItem`, `ListItemText`, `Collapse`, `Divider`

Add state:
```typescript
const discoverIndices = useDiscoverIndices();
const discoverSchema = useDiscoverSchema();
const [discoveredIndices, setDiscoveredIndices] = useState<DiscoverIndicesResponse | null>(null);
const [showConcreteIndices, setShowConcreteIndices] = useState(false);
const [discoveredSchema, setDiscoveredSchema] = useState<SchemaResponse | null>(null);
const [discoverError, setDiscoverError] = useState<string | null>(null);
```

Reset these in the `useEffect` when dialog opens (alongside existing resets).

**Step 3: Add "Discover" button and index list**

After the Index Pattern `TextField` in the Elasticsearch section (after line 272), add:
- A row with a "Discover Indices" `Button` (disabled if `esUrl` is empty or `discoverIndices.isPending`) and a `FormControlLabel` checkbox for "Show concrete indices"
- Follow the same `Box sx={{ display: 'flex', gap: 1 }}` pattern used by the LogFile Detect button
- `onClick` handler: build connection config from `esUrl`/`esUsername`/`esPassword` and call `discoverIndices.mutate(...)` with `onSuccess`/`onError`
- Below the button: render discovered aliases as clickable `Chip` components grouped under a "Aliases" `Typography` label, data streams under a "Data Streams" label, and optionally concrete indices
- Clicking a chip sets `esIndexPattern` to that name

**Step 4: Add "View Schema" button and field list**

After the index discovery section:
- A "View Schema" `Button` (disabled if `esIndexPattern` is empty)
- `onClick`: build full connection config and call `discoverSchema.mutate(...)`
- Below: a collapsible list of fields showing `name` and `type`, following the same display pattern used for LogFile detected fields (Chips with field names, lines 368-379)

**Step 5: Implementation details**

- Loading states: show `CircularProgress` (already imported) in button while pending
- Error states: show error `Alert` (already imported) if discovery fails
- Clear `discoveredIndices`/`discoveredSchema`/`discoverError` when `esUrl` changes or adapter type changes
- Build connection config for discovery using the corrected nested auth format

**Step 6: Verify frontend compiles and renders**

Run: `cd src/frontend && npx tsc --noEmit`
Expected: No errors

Run: `cd src/frontend && npm run build`
Expected: Build succeeds

**Step 7: Commit**

```bash
git add src/frontend/src/components/DataSourceDialog.tsx
git commit -m "feat: add inline ES index discovery and schema browsing to DataSourceDialog"
```

---

### Task 8: Final Verification

**Step 1: Run all backend tests**

Run: `dotnet test src/LogJammer.Tests/LogJammer.Tests.csproj`
Expected: All tests pass

**Step 2: Run frontend build**

Run: `cd src/frontend && npm run build`
Expected: Build succeeds

**Step 3: Run full application build**

Run: `dotnet build src/LogJammer.Api/LogJammer.Api.csproj`
Expected: Build succeeded

**Step 4: Commit any remaining changes and push**

```bash
git push
```
