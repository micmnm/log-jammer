# Elasticsearch Discovery UX Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add inline Elasticsearch index/alias discovery and schema browsing to the DataSource dialog, so users don't have to type config values blind.

**Architecture:** Two new POST endpoints on DataSourcesController accept raw connection config JSON (no saved data source needed) and proxy ES cluster APIs. The frontend adds discover/schema buttons to the Elasticsearch section of the existing DataSourceDialog.

**Tech Stack:** C# / ASP.NET Core 10, Elastic.Clients.Elasticsearch, React 19 / MUI v7 / TanStack Query v5

---

### Task 1: Add Discovery DTOs

**Files:**
- Modify: `src/LogJammer.Api/Dtos/DataSourceDtos.cs`

**Step 1: Add the new request/response DTOs to the existing file**

Add at the end of `src/LogJammer.Api/Dtos/DataSourceDtos.cs`:

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

**Step 1: Add `DiscoverIndicesAsync` method**

Add to `ElasticsearchAdapter` class:

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
            new Elastic.Clients.Elasticsearch.IndexManagement.GetDataStreamRequest(), cancellationToken);
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
Expected: Build succeeded. Note: The exact Elasticsearch client API shape for `GetAlias`, `GetDataStream`, and `Cat.Indices` may need adjustment based on the `Elastic.Clients.Elasticsearch` 9.x API. Check the client types if the build fails and adjust accordingly.

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

**Step 1: Add interface methods**

Add to `IDataSourceService`:

```csharp
Task<DiscoverIndicesResponse> DiscoverIndicesAsync(DiscoverIndicesRequest request, CancellationToken cancellationToken = default);
Task<SchemaResponse> DiscoverSchemaAsync(DiscoverSchemaRequest request, CancellationToken cancellationToken = default);
```

Add the `using LogJammer.Api.Dtos;` import if not already present (it should be).

**Step 2: Implement in DataSourceService**

Add to `DataSourceService`:

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

**Step 1: Add the two new endpoints**

Add to `DataSourcesController`:

```csharp
[HttpPost("discover/indices")]
public async Task<ActionResult<DiscoverIndicesResponse>> DiscoverIndices(
    [FromBody] DiscoverIndicesRequest request,
    CancellationToken cancellationToken)
{
    if (!ModelState.IsValid) return BadRequest(ModelState);

    try
    {
        var result = await dataSourceService.DiscoverIndicesAsync(request, cancellationToken);
        return Ok(result);
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

[HttpPost("discover/schema")]
public async Task<ActionResult<SchemaResponse>> DiscoverSchema(
    [FromBody] DiscoverSchemaRequest request,
    CancellationToken cancellationToken)
{
    if (!ModelState.IsValid) return BadRequest(ModelState);

    try
    {
        var result = await dataSourceService.DiscoverSchemaAsync(request, cancellationToken);
        return Ok(result);
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
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

**Step 1: Add TypeScript types**

Add to `src/frontend/src/api/types.ts`:

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

Add to `src/frontend/src/api/hooks/useDataSources.ts`:

```typescript
import type {
  // ... existing imports ...
  DiscoverIndicesRequest,
  DiscoverIndicesResponse,
  DiscoverSchemaRequest,
} from '../types';

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

**Step 1: Add discovery state and imports**

Add imports and state variables for:
- `useDiscoverIndices` and `useDiscoverSchema` hooks
- State: `discoveredIndices` (the response), `showConcreteIndices` toggle, `discoveredSchema` (fields array), `showSchema` toggle
- UI components: `Chip`, `List`, `ListItem`, `ListItemButton`, `ListItemText`, `Collapse`, `CircularProgress`, `Typography`, `Divider`, `IconButton`

**Step 2: Add "Discover" button and index list**

After the Index Pattern `TextField` in the Elasticsearch section, add:
- A row with a "Discover Indices" `Button` (disabled if `esUrl` is empty) and a `FormControlLabel` toggle for "Show concrete indices"
- When clicked, calls `discoverIndices.mutate(...)` with the current URL/auth config
- Below the button: a collapsible section showing aliases as `Chip` components grouped under an "Aliases" label, data streams under a "Data Streams" label, and optionally concrete indices
- Clicking an alias/data-stream/index chip sets `esIndexPattern` to that name

**Step 3: Add "View Schema" button and field list**

After the index discovery section, add:
- A "View Schema" `Button` (disabled if `esIndexPattern` is empty)
- When clicked, calls `discoverSchema.mutate(...)` with URL/auth/indexPattern config
- Below: a collapsible list of fields showing `name` and `type` as `ListItem` components

**Step 4: Implementation details**

The full implementation should handle:
- Loading states (show `CircularProgress` while discovering)
- Error states (show error `Alert` if discovery fails — e.g., wrong URL/auth)
- Clear discovered results when adapter type changes or URL changes
- Build connection config for discovery using existing `esUrl`, `esUsername`, `esPassword` state

**Step 5: Verify frontend compiles and renders**

Run: `cd src/frontend && npx tsc --noEmit`
Expected: No errors

Run: `cd src/frontend && npm run build`
Expected: Build succeeds

**Step 6: Commit**

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
