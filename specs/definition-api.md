# Definition API

Base URL: `http://localhost:5000`

## Health

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/health` | Application health check | Implemented |
| GET | `/healthz` | Infrastructure health check (DB) | Implemented |

## Data Sources

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/datasources` | List all data sources | Implemented |
| GET | `/api/datasources/{id}` | Get data source by ID | Implemented |
| POST | `/api/datasources` | Create data source | Implemented |
| PUT | `/api/datasources/{id}` | Update data source | Implemented |
| GET | `/api/datasources/{id}/deletion-impact` | Get cascade deletion impact counts | Implemented |
| DELETE | `/api/datasources/{id}?preserveHistory=false` | Delete data source (optionally preserve error groups) | Implemented |
| POST | `/api/datasources/{id}/test` | Test data source connection | Implemented |
| GET | `/api/datasources/{id}/schema` | Get data source schema | Implemented |
| GET | `/api/datasources/{id}/sample` | Get sample records (query: count) | Implemented |
| POST | `/api/datasources/detect` | Auto-detect log file format and propose config | Implemented |

**GET /api/datasources/{id}/deletion-impact**: Returns counts of all data that would be cascade-deleted: errorGroupCount, occurrenceCount, alertCount, classificationQueueCount, tagCount, ruleCount. Status codes: 200, 404.

**DELETE /api/datasources/{id}?preserveHistory=false**: When `preserveHistory=true`, detaches KnownErrors (sets DataSourceId=null) before deleting the DataSource, preserving error groups and their child data (occurrences, alerts, tags, overrides). When `preserveHistory=false` (default), cascade-deletes everything. Status codes: 204, 404.

**POST /api/datasources/detect**: Accepts `{ filePath: string }`. Reads up to 200 lines, detects JSON vs text format (>80% JSON parse threshold), infers timestamp/level/message field roles, returns proposed connection config. Path validated against allowed directories. Status codes: 200, 400 (empty file), 403 (path not allowed), 404 (file not found).

**LogFile connectionConfig format**: Uses singular `filePath` (not array). Includes `parseMode`, `timestampField`, `levelField`, `messageField`, `regexPattern` (when parseMode=regex).

## Error Groups

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/errorgroups` | List error groups (query: dataSourceId, status, severity, page, pageSize) | Implemented |
| GET | `/api/errorgroups/{id}` | Get error group detail by ID | Implemented |
| GET | `/api/errorgroups/{id}/occurrences` | Get occurrence history (query: from, to) | Implemented |
| PUT | `/api/errorgroups/{id}/status` | Update error group status | Implemented |
| PUT | `/api/errorgroups/{id}/severity` | Update error group severity | Implemented |

## Alerts

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/alerts` | List alerts (query: status, dataSourceId, page, pageSize) | Implemented |
| GET | `/api/alerts/{id}` | Get alert by ID | Implemented |
| POST | `/api/alerts/{id}/acknowledge` | Acknowledge an alert | Implemented |
| GET | `/api/alerts/history` | List resolved alerts (query: dataSourceId, page, pageSize) | Implemented |
| GET | `/api/alerts/correlated` | List correlated spike alerts (query: status, page, pageSize) | Implemented |

## Fingerprint Configs

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/datasources/{dataSourceId}/fingerprint-configs` | List fingerprint configs for data source | Implemented |
| POST | `/api/datasources/{dataSourceId}/fingerprint-configs` | Create fingerprint config | Implemented |
| GET | `/api/datasources/{dataSourceId}/fingerprint-configs/{id}` | Get fingerprint config by ID | Implemented |
| PUT | `/api/datasources/{dataSourceId}/fingerprint-configs/{id}` | Update fingerprint config | Implemented |
| DELETE | `/api/datasources/{dataSourceId}/fingerprint-configs/{id}` | Delete fingerprint config | Implemented |

## Spike Detection Rules

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/spikedetectionrules` | List all rules | Implemented |
| GET | `/api/spikedetectionrules/{id}` | Get rule by ID | Implemented |
| POST | `/api/spikedetectionrules` | Create a rule | Implemented |
| PUT | `/api/spikedetectionrules/{id}` | Update a rule | Implemented |
| DELETE | `/api/spikedetectionrules/{id}` | Delete a rule | Implemented |

## Tags

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/tags` | List all tags | Implemented |
| GET | `/api/tags/{id}` | Get tag by ID | Implemented |
| POST | `/api/tags` | Create a tag | Implemented |
| PUT | `/api/tags/{id}` | Update a tag | Implemented |
| DELETE | `/api/tags/{id}` | Delete a tag | Implemented |

## Configuration

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/configuration` | Get all classification config key/values | Implemented |
| PUT | `/api/configuration` | Update a configuration value | Implemented |

## Classification Queue

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/classification/queue` | List pending classification items (query: page, pageSize). Response includes error context: severity, status, firstSeen, lastSeen, totalOccurrences from KnownError | Implemented |
| GET | `/api/classification/queue/{id}` | Get single classification queue item (includes error context fields) | Implemented |
| POST | `/api/classification/queue/{id}/approve` | Accept suggested tags (or user-assigned tags for unmatched items) | Implemented |
| POST | `/api/classification/queue/{id}/reject` | Reject with user-provided tags and optional reason | Implemented |

## Static Files & SPA

| Path | Description |
|------|-------------|
| `/` | Serves `wwwroot/index.html` (React SPA entry point) |
| `/assets/*` | Static frontend assets (JS, CSS) from `wwwroot/` |
| `/*` (non-API, non-file) | Falls back to `index.html` for client-side routing |

Middleware order: `UseDefaultFiles()` → `UseStaticFiles()` → `MapControllers()` → `MapFallbackToFile("index.html")`

## CORS

| Origin | Methods | Status |
|--------|---------|--------|
| `http://localhost:5173` | All | Implemented (dev policy) |

## OpenAPI

| Path | Description |
|------|-------------|
| `/openapi/v1.json` | OpenAPI 3.0 specification |
| `/scalar/v1` | Scalar API reference UI |
