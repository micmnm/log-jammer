# SampleLog Mock Elasticsearch Server — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Embed a mock Elasticsearch HTTP server in SampleLog so users can register it as an ES data source in LogJammer without a real ES cluster.

**Architecture:** ASP.NET Core Minimal API (`WebApplication`) runs on `http://localhost:9200` as a background task alongside the Terminal.Gui TUI. It exposes 3 endpoints (`GET /`, `POST /{index}/_search`, `GET /{index}/_mapping`) that return ES 8.x-compatible JSON responses, serving data from the LogGenerator's JSON log file.

**Tech Stack:** ASP.NET Core Minimal API (framework reference), System.Text.Json, Terminal.Gui (existing)

---

### Task 1: Add ASP.NET Core Framework Reference

**Files:**
- Modify: `src/SampleLog/SampleLog.csproj`

**Step 1: Add the framework reference**

In `src/SampleLog/SampleLog.csproj`, add a `<FrameworkReference>` inside a new `<ItemGroup>` after the existing `<PackageReference>` group:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

**Step 2: Verify it builds**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/SampleLog/SampleLog.csproj
git commit -m "chore(samplelog): add ASP.NET Core framework reference for mock ES server"
```

---

### Task 2: Create MockElasticsearchServer

**Files:**
- Create: `src/SampleLog/MockElasticsearch/MockElasticsearchServer.cs`

This class wraps a `WebApplication` that listens on port 9200. It reads log entries from the LogGenerator's JSON file to serve search results.

**Step 1: Create the file**

Create `src/SampleLog/MockElasticsearch/MockElasticsearchServer.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using SampleLog.Generation;

namespace SampleLog.MockElasticsearch;

public sealed class MockElasticsearchServer : IAsyncDisposable
{
    private const int Port = 9200;
    private const string IndexName = "sample-logs";
    public static string Url => $"http://localhost:{Port}";
    public static string IndexPattern => IndexName;

    private readonly WebApplication _app;
    private readonly LogGenerator _generator;

    public MockElasticsearchServer(LogGenerator generator)
    {
        _generator = generator;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(Url);
        builder.Logging.ClearProviders(); // keep TUI clean

        _app = builder.Build();
        MapEndpoints();
    }

    public Task StartAsync() => _app.StartAsync();

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private void MapEndpoints()
    {
        // GET / — Ping / cluster info
        _app.MapGet("/", () => Results.Json(new
        {
            name = "sample-log-mock",
            cluster_name = "samplelog",
            cluster_uuid = "mock-uuid",
            version = new
            {
                number = "8.15.0",
                build_flavor = "default",
                build_type = "docker",
                lucene_version = "9.11.1"
            },
            tagline = "You Know, for Search"
        }));

        // POST /{index}/_search — Search logs
        _app.MapPost("/{index}/_search", async (HttpContext ctx, string index) =>
        {
            var body = await ParseRequestBody(ctx);
            var size = body?["size"]?.GetValue<int>() ?? 10;
            DateTime? gte = null;

            // Parse range query on @timestamp
            var rangeNode = body?["query"]?["range"];
            if (rangeNode is JsonObject rangeObj)
            {
                foreach (var (_, fieldValue) in rangeObj)
                {
                    var gteStr = fieldValue?["gte"]?.GetValue<string>();
                    if (gteStr is not null && DateTime.TryParse(gteStr, out var parsed))
                        gte = parsed;
                }
            }

            var entries = ReadLogEntries(size, gte);
            var hits = entries.Select(e => new { _index = IndexName, _id = Guid.NewGuid().ToString(), _source = e }).ToList();

            return Results.Json(new
            {
                took = 1,
                timed_out = false,
                _shards = new { total = 1, successful = 1, skipped = 0, failed = 0 },
                hits = new
                {
                    total = new { value = hits.Count, relation = "eq" },
                    max_score = (double?)null,
                    hits
                }
            });
        });

        // GET /{index}/_mapping — Field mappings
        _app.MapGet("/{index}/_mapping", (string index) =>
        {
            var properties = new Dictionary<string, object>
            {
                ["@timestamp"] = new { type = "date" },
                ["level"] = new { type = "keyword" },
                ["message"] = new { type = "text" },
                ["service"] = new { type = "keyword" },
                ["exception"] = new { type = "text" },
                ["timestamp"] = new { type = "date" }
            };

            // Add dynamic properties from known templates
            foreach (var template in _generator.Library.Templates)
            {
                if (template.Properties is null) continue;
                foreach (var propName in template.Properties.Keys)
                {
                    if (!properties.ContainsKey(propName))
                    {
                        // Heuristic: if any value is a number, use long; otherwise keyword
                        var values = template.Properties[propName];
                        var isNumeric = values.Any(v => v is JsonElement el && el.ValueKind == JsonValueKind.Number);
                        properties[propName] = new { type = isNumeric ? "long" : "keyword" };
                    }
                }
            }

            var mapping = new Dictionary<string, object>
            {
                [IndexName] = new
                {
                    mappings = new { properties }
                }
            };

            return Results.Json(mapping);
        });
    }

    private List<JsonObject> ReadLogEntries(int count, DateTime? since)
    {
        var entries = new List<JsonObject>();
        var filePath = _generator.JsonFilePath;

        if (!File.Exists(filePath))
            return entries;

        // Read all lines and take the last N (most recent)
        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (IOException)
        {
            return entries;
        }

        // Process in reverse order (newest first) to match ES default desc sort
        for (var i = lines.Length - 1; i >= 0 && entries.Count < count; i--)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var obj = JsonNode.Parse(line)?.AsObject();
                if (obj is null) continue;

                // Apply timestamp filter if present
                if (since.HasValue)
                {
                    var tsStr = obj["timestamp"]?.GetValue<string>();
                    if (tsStr is not null && DateTime.TryParse(tsStr, out var ts) && ts < since.Value)
                        continue;
                }

                // Add @timestamp field mirroring timestamp (ES convention)
                var timestamp = obj["timestamp"]?.GetValue<string>();
                if (timestamp is not null && !obj.ContainsKey("@timestamp"))
                    obj["@timestamp"] = timestamp;

                entries.Add(obj);
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }
        }

        return entries;
    }

    private static async Task<JsonObject?> ParseRequestBody(HttpContext ctx)
    {
        try
        {
            var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            return JsonNode.Parse(doc.RootElement.GetRawText())?.AsObject();
        }
        catch
        {
            return null;
        }
    }
}
```

**Step 2: Verify it builds**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/SampleLog/MockElasticsearch/MockElasticsearchServer.cs
git commit -m "feat(samplelog): add mock Elasticsearch server with ping, search, mapping endpoints"
```

---

### Task 3: Start Mock ES Server in Program.cs

**Files:**
- Modify: `src/SampleLog/Program.cs`

**Step 1: Wire up the mock server**

The mock server must start before the TUI and stop after. Update `src/SampleLog/Program.cs`:

Add at the top:
```csharp
using SampleLog.MockElasticsearch;
```

After `var runner = new ScenarioRunner();` and before `Application.Init();`, add:
```csharp
await using var mockEs = new MockElasticsearchServer(generator);
await mockEs.StartAsync();
```

The full file should look like:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SampleLog.Generation;
using SampleLog.MockElasticsearch;
using SampleLog.Models;
using SampleLog.UI;
using Terminal.Gui;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var outputConfig = new OutputConfig();
config.GetSection("Output").Bind(outputConfig);

var defaults = new DefaultsConfig();
config.GetSection("Defaults").Bind(defaults);

var apiConfig = new LogJammerApiConfig();
config.GetSection("LogJammerApi").Bind(apiConfig);

var libraryJson = await File.ReadAllTextAsync("log-library.json");
var library = JsonSerializer.Deserialize<LogLibrary>(libraryJson)
    ?? throw new InvalidOperationException("Failed to load log-library.json");

using var generator = new LogGenerator(library, outputConfig);
var runner = new ScenarioRunner();

await using var mockEs = new MockElasticsearchServer(generator);
await mockEs.StartAsync();

Application.Init();
var mainWindow = new MainWindow(generator, runner, defaults, apiConfig);
Application.Run(mainWindow);
mainWindow.Dispose();
Application.Shutdown();
```

**Step 2: Verify it builds and runs**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

Quick smoke test (optional manual): `dotnet run --project src/SampleLog` then in another terminal: `curl http://localhost:9200/` — should return mock ES cluster info JSON.

**Step 3: Commit**

```bash
git add src/SampleLog/Program.cs
git commit -m "feat(samplelog): start mock ES server alongside TUI"
```

---

### Task 4: Update TUI Menu to Show Mock ES URL

**Files:**
- Modify: `src/SampleLog/UI/MainWindow.cs`

**Step 1: Add the mock ES info line**

In `MainWindow.cs`, in the constructor where the menu labels are created (around line 79-91), add a new label showing the mock ES URL. Insert after `logPathLabel`:

Replace:
```csharp
var logPathLabel = new Label
{
    Text = $"  Log: {_generator.JsonFilePath}",
    X = 0, Y = 0, Width = Dim.Fill()
};
var sep = new Label { Text = new string('=', 120), X = 0, Y = 1, Width = Dim.Fill() };
```

With:
```csharp
var logPathLabel = new Label
{
    Text = $"  Log: {_generator.JsonFilePath}",
    X = 0, Y = 0, Width = Dim.Fill()
};
var esLabel = new Label
{
    Text = $"  Mock ES: {MockElasticsearchServer.Url}/{MockElasticsearchServer.IndexPattern}",
    X = 0, Y = 1, Width = Dim.Fill()
};
var sep = new Label { Text = new string('=', 120), X = 0, Y = 2, Width = Dim.Fill() };
```

Then shift all subsequent Y positions down by 1:
- `row1`: Y = 2 → Y = 3
- `row2`: Y = 3 → Y = 4
- `row3`: Y = 4 → Y = 5
- `row4`: Y = 5 → Y = 6
- `row5`: Y = 6 → Y = 7
- `sep2`: Y = 7 → Y = 8

Add the `esLabel` to the `menuView.Add(...)` call.

Add `using SampleLog.MockElasticsearch;` at the top of the file.

**Step 2: Verify it builds**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/SampleLog/UI/MainWindow.cs
git commit -m "feat(samplelog): show mock ES URL in TUI menu area"
```

---

### Task 5: Add Elasticsearch Option to [R] Register Dialog

**Files:**
- Modify: `src/SampleLog/UI/MainWindow.cs`

**Step 1: Add the [4] Elasticsearch button to ShowRegisterDialog**

In the `ShowRegisterDialog()` method (around line 575), add a 4th button for Elasticsearch:

Replace:
```csharp
var label = new Label { Text = "Register which log file?", X = 1, Y = 1 };
var jsonBtn = new Button { Text = "[1] JSON", X = 1, Y = 3 };
var textBtn = new Button { Text = "[2] Text", X = 14, Y = 3 };
var bothBtn = new Button { Text = "[3] Both", X = 27, Y = 3 };
var cancelBtn = new Button { Text = "Cancel" };

string? choice = null;
jsonBtn.Accepting += (s, e) => { e.Cancel = true; choice = "json"; Application.RequestStop(); };
textBtn.Accepting += (s, e) => { e.Cancel = true; choice = "text"; Application.RequestStop(); };
bothBtn.Accepting += (s, e) => { e.Cancel = true; choice = "both"; Application.RequestStop(); };
cancelBtn.Accepting += (s, e) => { e.Cancel = true; Application.RequestStop(); };

dialog.Add(label, jsonBtn, textBtn, bothBtn);
```

With:
```csharp
var label = new Label { Text = "Register which data source?", X = 1, Y = 1 };
var jsonBtn = new Button { Text = "[1] JSON", X = 1, Y = 3 };
var textBtn = new Button { Text = "[2] Text", X = 14, Y = 3 };
var bothBtn = new Button { Text = "[3] Both", X = 27, Y = 3 };
var esBtn = new Button { Text = "[4] Elasticsearch", X = 1, Y = 5 };
var cancelBtn = new Button { Text = "Cancel" };

string? choice = null;
jsonBtn.Accepting += (s, e) => { e.Cancel = true; choice = "json"; Application.RequestStop(); };
textBtn.Accepting += (s, e) => { e.Cancel = true; choice = "text"; Application.RequestStop(); };
bothBtn.Accepting += (s, e) => { e.Cancel = true; choice = "both"; Application.RequestStop(); };
esBtn.Accepting += (s, e) => { e.Cancel = true; choice = "elasticsearch"; Application.RequestStop(); };
cancelBtn.Accepting += (s, e) => { e.Cancel = true; Application.RequestStop(); };

dialog.Add(label, jsonBtn, textBtn, bothBtn, esBtn);
```

Also increase dialog height from 10 to 12 to accommodate the new button row.

**Step 2: Handle the elasticsearch choice in RegisterAsync**

In the `RegisterAsync` method, add handling for the `"elasticsearch"` choice. Before the `using var http = ...` line, add an early return branch:

```csharp
if (mode == "elasticsearch")
{
    await RegisterElasticsearchAsync();
    return;
}
```

**Step 3: Add the RegisterElasticsearchAsync method**

Add a new method after `RegisterAsync`:

```csharp
private async void RegisterElasticsearchAsync()
{
    using var http = new HttpClient { BaseAddress = new Uri(_apiConfig.BaseUrl) };

    try
    {
        var connectionConfig = JsonSerializer.Serialize(new
        {
            url = MockElasticsearchServer.Url,
            indexPattern = MockElasticsearchServer.IndexPattern
        });

        var createPayload = JsonSerializer.Serialize(new
        {
            name = "SampleLog Elasticsearch",
            adapterType = "Elasticsearch",
            connectionConfig,
            pollIntervalSeconds = 30,
            enabled = true
        });

        var response = await http.PostAsync("/api/datasources",
            new StringContent(createPayload, System.Text.Encoding.UTF8, "application/json"));

        if (response.IsSuccessStatusCode)
            AddStatusLine("INF  [register] SampleLog Elasticsearch registered successfully");
        else
        {
            var err = await response.Content.ReadAsStringAsync();
            AddStatusLine($"ERR  [register] ES registration failed: {err}");
        }
    }
    catch (Exception ex)
    {
        AddStatusLine($"ERR  [register] {ex.Message}");
    }
}
```

**Step 4: Verify it builds**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add src/SampleLog/UI/MainWindow.cs
git commit -m "feat(samplelog): add Elasticsearch option to register dialog"
```

---

### Task 6: Manual Integration Test

**No files to change — this is a verification step.**

**Step 1: Start LogJammer backend**

Run: `dotnet run --project src/LogJammer.Api` (or `docker compose up`)

**Step 2: Start SampleLog**

Run: `dotnet run --project src/SampleLog`

**Step 3: Verify mock ES ping**

In another terminal:
```bash
curl http://localhost:9200/
```
Expected: JSON with `"cluster_name": "samplelog"`, `"tagline": "You Know, for Search"`

**Step 4: Verify mock ES mapping**

```bash
curl http://localhost:9200/sample-logs/_mapping
```
Expected: JSON with `sample-logs.mappings.properties` containing `@timestamp`, `level`, `message`, etc.

**Step 5: Verify mock ES search**

```bash
curl -X POST http://localhost:9200/sample-logs/_search -H 'Content-Type: application/json' -d '{"size": 5}'
```
Expected: JSON with `hits.hits` array containing log entries from the generated file.

**Step 6: Register via TUI**

In the SampleLog TUI, press `R`, then `[4] Elasticsearch`. Check the log output for success message.

**Step 7: Verify in LogJammer**

In LogJammer (API or frontend), check that `SampleLog Elasticsearch` data source exists and can test connection successfully.
