# SampleLog Mock Elasticsearch Server

## Goal
Add a mock Elasticsearch HTTP server to SampleLog so users can register it as an ES data source in LogJammer without needing a real Elasticsearch cluster.

## Design

### Mock ES Server (`src/SampleLog/MockElasticsearch/MockElasticsearchServer.cs`)

ASP.NET Core Minimal API (`WebApplication`) running on `http://localhost:9200` as a background task.

Exposes 3 endpoints matching what LogJammer's `ElasticsearchAdapter` calls:

| Endpoint | Purpose | Response |
|---|---|---|
| `GET /` | Ping/health check | ES-compatible root response with cluster info |
| `POST /{index}/_search` | Search logs | ES-compatible search response with hits from generated log data |
| `GET /{index}/_mapping` | Field mappings | ES-compatible mapping response with log field types |

**Fixed configuration:**
- Port: 9200
- Index name: `sample-logs`
- No authentication

**Search endpoint details:**
- Reads from LogGenerator's JSON log file (tail-reads for latest entries)
- Respects `size` parameter from search request body
- Respects `query.range.@timestamp.gte` for time-based filtering
- Returns results sorted by `@timestamp` descending
- Response format matches ES 8.x search response structure

**Mapping response:**
- Fields: `@timestamp` (date), `level` (keyword), `message` (text), `service` (keyword), `exception` (text), plus any template properties as keyword/text

### TUI Changes

1. **Menu area:** Add line showing `Mock ES: http://localhost:9200/sample-logs`
2. **[R] Register dialog:** Add 4th button `[4] Elasticsearch` that calls `POST /api/datasources` with:
   - `name: "SampleLog Elasticsearch"`
   - `adapterType: "Elasticsearch"`
   - `connectionConfig: {"url":"http://localhost:9200","indexPattern":"sample-logs"}`

### Project Changes

- Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `SampleLog.csproj`
- New file: `MockElasticsearch/MockElasticsearchServer.cs`
- Modified: `Program.cs` (start mock server), `UI/MainWindow.cs` (menu + register)

## Implementation Steps

1. Add ASP.NET Core framework reference to SampleLog.csproj
2. Create `MockElasticsearchServer` class with the 3 endpoints
3. Start mock server in Program.cs before TUI
4. Update MainWindow menu area to show mock ES URL
5. Add Elasticsearch option to [R] Register dialog
6. Test: verify LogJammer can connect, poll, and get schema from mock ES
