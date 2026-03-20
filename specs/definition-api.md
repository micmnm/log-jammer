# Definition API

Base URL: `http://localhost:5050`

## Authentication

| Method | Path | Auth required | Description |
|--------|------|---------------|-------------|
| POST | `/api/auth/login` | No | Login with password, returns bearer token |

**POST /api/auth/login**
- Body: `LoginRequest` — `{ "password": "..." }`
- 200: `LoginResponse` — `{ "token": "..." }`
- 401: `{ "message": "Invalid password" }`

**Token usage**: All other `/api/*` endpoints require one of:
- `Authorization: Bearer <token>` — token from `/api/auth/login`
- `X-Api-Key: <key>` — static API key configured in `appsettings.json` (`Auth__ApiKey`)

Unauthenticated requests to `/api/*` (except `/api/auth/login`) receive `401 Unauthorized`.

---

## Health

| Method | Path | Description |
|--------|------|-------------|
| GET | `/healthz` | Returns `"ok"` (no auth required) |

---

## Data Sources

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/datasources` | List all data sources, ordered by name |
| GET | `/api/datasources/{id}` | Get data source by ID |
| POST | `/api/datasources` | Create a new data source |
| PUT | `/api/datasources/{id}` | Update a data source (partial update) |
| DELETE | `/api/datasources/{id}` | Delete a data source |
| POST | `/api/datasources/{id}/test` | Test Elasticsearch connection |

**GET /api/datasources**
- 200: `DataSourceResponse[]`

**GET /api/datasources/{id}**
- 200: `DataSourceResponse`
- 404

**POST /api/datasources**
- Body: `CreateDataSourceRequest`
- 201: `DataSourceResponse` (Location header set)

**PUT /api/datasources/{id}**
- Body: `UpdateDataSourceRequest` — null fields are ignored
- 200: `DataSourceResponse`
- 404

**DELETE /api/datasources/{id}**
- 204
- 404

**POST /api/datasources/{id}/test**
- Only supported for `Elasticsearch` type data sources
- 200: `{ "success": true }` or `{ "success": false, "message": "..." }`
- 400: `{ "message": "Connection test is only supported for Elasticsearch data sources" }`
- 404

---

## Patterns

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/patterns` | List patterns (paged, filterable) |
| GET | `/api/patterns/{id}` | Get pattern detail with occurrence history and baseline bands |
| POST | `/api/patterns/{id}/acknowledge` | Mark a pattern as not new |
| POST | `/api/patterns/acknowledge-all` | Acknowledge all new patterns (optionally scoped to a data source) |

**GET /api/patterns**

Query parameters:
- `page` (int, default 1)
- `pageSize` (int, default 50)
- `dataSourceId` (Guid?)
- `severity` (Severity?)
- `isNew` (bool?)
- `search` (string?) — case-insensitive template substring match

- 200: `PagedResult<PatternListItem>`

**GET /api/patterns/{id}**
- 200: `PatternDetailResponse` — includes last 168h of occurrences and all baseline bands
- 404

**POST /api/patterns/{id}/acknowledge**
- 204
- 404

**POST /api/patterns/acknowledge-all**

Query parameters:
- `dataSourceId` (Guid?) — if omitted, acknowledges all new patterns across all data sources

- 200: `{ "acknowledged": <count> }`

---

## Dashboard

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/dashboard` | Summary stats, top anomalies, and newly seen patterns |

**GET /api/dashboard**
- 200: `DashboardResponse`
  - `totalPatterns` — total distinct patterns in DB
  - `newPatternCount` — patterns where `IsNew = true`
  - `ingestionRatePerHour` — sum of occurrence counts in the current hour window
  - `topAnomalies` — up to 10 patterns with |StdDevsFromMean| > 1.0, ordered by deviation descending
  - `newPatterns` — up to 50 most recently first-seen patterns where `IsNew = true`

---

## Ingest (Push)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/ingest/{dataSourceId}` | Push log entries into a data source |

**POST /api/ingest/{dataSourceId}**
- Body: `IngestRequest` — array of up to 10 000 `IngestEntry` items
- 200: `IngestResponse` — `{ "accepted": <count> }`
- 400: `{ "message": "Data source is disabled" }`
- 404: `{ "message": "Data source not found" }`

Notes: Works for both `KibanaProxy` and `Elasticsearch` data source types. The ingestion pipeline runs DrainParser, updates `PatternOccurrence` windows, and stores a new pattern if `IsNewCluster = true`.

---

## Static Files & SPA

| Path | Description |
|------|-------------|
| `/` | Serves `wwwroot/index.html` (React SPA entry point) |
| `/assets/*` | Static frontend assets from `wwwroot/` |
| `/*` (non-API, non-file) | Falls back to `index.html` for client-side routing |

Middleware order: `UseDefaultFiles()` → `UseStaticFiles()` → `MapControllers()` → `MapFallbackToFile("index.html")`

---

## CORS

| Origin | Methods | Enabled |
|--------|---------|---------|
| `http://localhost:5173` | All | Development only (`DevCors` policy) |

---

## OpenAPI

Available in development only.

| Path | Description |
|------|-------------|
| `/openapi/v1.json` | OpenAPI 3.0 specification |
| `/scalar/v1` | Scalar API reference UI |
