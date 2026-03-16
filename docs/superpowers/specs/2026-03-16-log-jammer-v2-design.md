# Log Jammer v2 — Design Spec

## Overview

Log Jammer v2 is a lean log monitoring tool that ingests logs from Kibana (via Chrome extension) and Elasticsearch (direct API), discovers log patterns using the Drain algorithm, and surfaces new patterns and rate anomalies against historical baselines.

V1 was over-engineered (15+ entities, ONNX embeddings, pgvector, complex classification pipeline) and the core detection mechanism (fingerprint heuristics + embedding similarity) didn't work well. V2 replaces all of that with a proven log parsing algorithm (Drain) and simple statistical baselines.

## Goals

- Detect new error patterns as they appear
- Show per-pattern message rates vs historical baseline ("47/hr now, usually ~5/hr")
- Ingest from Kibana (restricted access) and Elasticsearch (direct)
- Stay lean: 5 entities, 2 projects, ~12 endpoints, 3 frontend pages

## Non-Goals

- Alerting / notification system (future)
- ML classification, embeddings, tag management
- Log file adapter, PostgreSQL adapter (future)
- Adaptive sampling

---

## Architecture

```
+---------------------+     +----------------------+
|  Chrome Extension   |     |  Log Jammer Frontend |
|  (Kibana Bridge)    |     |  (React 19 + MUI 7)  |
|                     |     |                       |
|  - Intercept queries|     |  - Dashboard          |
|  - Field selector   |     |  - Data Sources config|
|  - Auto-detect ts/lv|     |  - Pattern Detail     |
|  - Batch push       |     |                       |
+--------+------------+     +--------+--------------+
         | POST /api/ingest          | REST API
         v                           v
+------------------------------------------------+
|  LogJammer.Api                                  |
|  - ~10 endpoints                                |
|  - ElasticsearchPollingService (background)     |
|  - BaselineRecalculationService (background)    |
+---------------------+--------------------------+
                      |
                      v
+------------------------------------------------+
|  LogJammer.Engine                               |
|  - DrainParser (C# port)                        |
|  - PatternStore (EF Core)                       |
|  - BaselineCalculator                           |
|  - IngestionPipeline                            |
|  - StackTracePreprocessor                       |
+---------------------+--------------------------+
                      |
                      v
+------------------------------------------------+
|  PostgreSQL                                     |
|  - log_patterns, pattern_occurrences,           |
|    pattern_baselines, data_sources,             |
|    drain_states                                 |
+------------------------------------------------+
```

### Projects

- **LogJammer.Engine** — Drain algorithm, pattern storage, baseline calculation, ingestion pipeline, stack trace preprocessing. Depends on EF Core + PostgreSQL. No HTTP concerns.
- **LogJammer.Api** — ASP.NET Core host, controllers, DTOs, background services. Wires HTTP to Engine calls.

No Core/Infrastructure split. No repository pattern. Engine uses EF Core directly.

---

## Data Model

### DataSource

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| Name | string | User-provided name |
| Type | enum | KibanaProxy, Elasticsearch |
| ConnectionConfig | jsonb | Adapter-specific config |
| MessageTemplate | string? | Field aggregation template, e.g., `"{service.name} \| {error.type} \| {message}"` |
| Enabled | bool | Toggle polling on/off |
| CreatedAt | DateTimeOffset | |
| LastPolledAt | DateTimeOffset? | |

### DrainState

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| DataSourceId | Guid | FK to DataSource (unique) |
| SerializedState | byte[] | Drain parse tree, serialized. Expected size: ~1-5 KB per 100 clusters. |
| UpdatedAt | DateTimeOffset | Last time state was persisted |

One DrainParser instance per DataSource. Each data source gets its own parse tree so patterns from different sources don't bleed into each other. State is persisted after each ingestion batch and restored on startup.

**ConnectionConfig by type:**

- **Elasticsearch:** `{ url, indexPattern, auth? { username, password }, pollingIntervalSeconds }`
- **KibanaProxy:** `{ kibanaUrl, proxyEndpoint, queryDsl, fullRequestBody }` (set by Chrome extension during subscribe)

### LogPattern

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| Template | string | Drain-extracted pattern, e.g., `"payment-service \| TimeoutException \| Connection to * timed out after *ms"` |
| ClusterId | int | Drain's internal cluster ID. Used for fast lookup during ingestion; Template string is the canonical identity. If Drain evicts a cluster, the LogPattern row remains (orphaned from Drain tree but still queryable). |
| FirstSeen | DateTimeOffset | |
| LastSeen | DateTimeOffset | |
| SampleMessage | string | One real log line that matched |
| Severity | enum | Info, Warning, Error, Critical. Mapped from log level: Debug/Trace→Info, Warn/Warning→Warning, Error→Error, Fatal/Critical→Critical. Unknown/null→Info. |
| DataSourceId | Guid | FK to DataSource |
| IsNew | bool | True until user acknowledges |

### PatternOccurrence

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| PatternId | Guid | FK to LogPattern |
| WindowStart | DateTimeOffset | 1-hour window start |
| WindowEnd | DateTimeOffset | 1-hour window end |
| Count | long | Messages in this window |

Index: `(PatternId, WindowStart)` unique.

Windows are clock-aligned to UTC hours (e.g., 14:00-15:00 UTC). Hour-of-week calculations in PatternBaseline also use UTC.

### PatternBaseline

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| PatternId | Guid | FK to LogPattern |
| HourOfWeek | int | 0-167 (hour within the week, captures day-of-week + time-of-day) |
| AvgCount | double | Average count for this hour slot |
| StdDevCount | double | Standard deviation |

Index: `(PatternId, HourOfWeek)` unique.

Recalculated periodically from PatternOccurrence data. Enables comparison: "this error normally happens 5x/hr on Monday 2pm, today it's 50x."

---

## Engine Components

### DrainParser

C# port of the Drain3 algorithm (IBM). Builds a fixed-depth parse tree from log messages, extracting templates by replacing variable tokens with `*`.

**Public API:**
- `ParseLogMessage(string message) -> DrainResult { ClusterId, Template, IsNewCluster }`
- `GetState() -> byte[]` — serialize parse tree for persistence
- `RestoreState(byte[])` — restore from persistence

**Configuration:**
- `SimilarityThreshold` (double, default 0.4) — minimum similarity to merge into existing cluster
- `MaxClusters` (int, default 1000) — max clusters before eviction
- `TreeDepth` (int, default 4) — parse tree depth

**Scoping:** One DrainParser instance per DataSource. Patterns from different sources stay isolated. State serialized to `DrainState` table after each ingestion batch, restored on startup.

**Eviction policy:** When `MaxClusters` is reached, the least recently matched cluster is evicted (LRU). The corresponding LogPattern row remains in the database — it just won't receive new matches from Drain. This is acceptable: old patterns stay queryable for historical analysis.

**Concurrency:** IngestionPipeline acquires a per-DataSource lock (in-memory semaphore) so concurrent poll/push for the same source are serialized. Different data sources process in parallel.

### StackTracePreprocessor

Pre-processes fields identified as stack traces before Drain parsing.

- Detects stack trace fields **by field name**: field name contains "stack", "trace", or "exception" (case-insensitive). Does NOT use content heuristics to avoid false positives on normal messages.
- Extracts top 3 frames
- Strips line numbers, memory addresses, file paths
- Returns cleaned summary: `"at PaymentService.Process > DatabaseClient.Execute > NpgsqlConnection.Open"`

Runs as a step in IngestionPipeline, before Drain. Invisible to the user — they just select "include stack_trace" and the engine handles noise reduction.

### IngestionPipeline

Entry point for all log data, shared by Elasticsearch polling and Chrome extension push.

**Flow:**
1. Receive `RawLogEntry[]` (message, timestamp, level, raw fields dict)
2. Apply `MessageTemplate` — substitute field references, combine into single string
3. Run `StackTracePreprocessor` on fields detected as stack traces
4. Feed combined message into `DrainParser` → get `DrainResult`
5. Upsert `LogPattern` (create if `IsNewCluster`, update `LastSeen` + `SampleMessage` if existing)
6. Increment `PatternOccurrence` for current 1-hour window

**RawLogEntry:**
```
{ Message: string, Timestamp: DateTimeOffset, Level: string?, Fields: Dictionary<string, string>? }
```

When MessageTemplate is configured, Fields are used for substitution. When not configured, Message is used directly.

### PatternStore

Manages pattern lifecycle via EF Core directly (no repository abstraction).

- `RecordOccurrence(DrainResult, severity, rawMessage, dataSourceId, timestamp)` — upserts LogPattern, increments hourly bucket
- `GetPatterns(dataSourceId?, severity?, isNew?, timeRange?)` — filtered pattern list with current-window count
- `GetPatternDetail(patternId)` — pattern + occurrence history + baseline comparison
- `AcknowledgePattern(patternId)` — sets IsNew = false

### BaselineCalculator

Computes historical rate statistics per pattern per hour-of-week.

- `RecalculateBaselines(patternId?)` — aggregates PatternOccurrence into PatternBaseline (rolling avg + stddev per hour-of-week slot, using last 4 weeks of data)
- `GetCurrentComparison(patternId) -> { CurrentRate, ExpectedRate, StdDevsFromMean }` — "47/hr now, usually 5/hr (8.4 sigma)"

Runs as a background task every hour.

---

## API Endpoints

### Data Sources

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/datasources | List all data sources |
| GET | /api/datasources/{id} | Get single data source |
| POST | /api/datasources | Create data source |
| PUT | /api/datasources/{id} | Update data source |
| DELETE | /api/datasources/{id} | Delete data source |
| POST | /api/datasources/{id}/test | Test connection (ES only) |
| GET | /api/datasources/{id}/fields | Discover available fields from ES index (for message template picker). Runs a sample query, returns union of _source field names. |

### Ingest

| Method | Path | Description |
|--------|------|-------------|
| POST | /api/ingest/{dataSourceId} | Push log entries (batch). Max 10,000 entries. |

**Request body:**
```json
{
  "entries": [
    {
      "message": "payment-service | TimeoutException | Connection to db timed out after 5000ms",
      "timestamp": "2026-03-16T14:32:01Z",
      "level": "Error"
    }
  ]
}
```

The Chrome extension applies the message template client-side: it extracts the user-selected fields from each ES hit, combines them per the template, and sends the pre-combined `message` string. The backend does NOT need field-level knowledge for push ingest — it receives ready-to-parse messages.

For Elasticsearch polling, the backend applies the message template server-side (it has access to the raw ES response fields).

### Patterns

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/patterns | List patterns. Filters: dataSourceId, severity, isNew, timeRange. Paginated (page, pageSize, default 50). Returns template, severity, firstSeen, lastSeen, currentRate, expectedRate, deviation. |
| GET | /api/patterns/{id} | Pattern detail: template, sample message, occurrence history, baseline comparison chart data. |
| POST | /api/patterns/{id}/acknowledge | Mark pattern as not new. |
| POST | /api/patterns/acknowledge-all | Bulk acknowledge all new patterns. Optional filter: dataSourceId. Returns `{ acknowledged: int }`. |

### Dashboard

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/dashboard | Summary object (see below). |

**Dashboard response:**
```json
{
  "totalPatterns": 142,
  "newPatternCount": 3,
  "ingestionRatePerHour": 12450,
  "topAnomalies": [
    {
      "patternId": "guid",
      "template": "payment-service | TimeoutException | Connection to * timed out...",
      "severity": "Error",
      "currentRate": 47,
      "expectedRate": 5.2,
      "stdDevsFromMean": 8.4,
      "dataSourceName": "prod-kibana"
    }
  ],
  "newPatterns": [
    {
      "patternId": "guid",
      "template": "...",
      "severity": "Warning",
      "firstSeen": "2026-03-16T14:00:00Z",
      "dataSourceName": "prod-kibana"
    }
  ]
}
```
`topAnomalies`: top 10 by stdDevsFromMean (descending). `newPatterns`: all with IsNew=true, max 50.

---

## Background Services

### ElasticsearchPollingService

- Polls enabled Elasticsearch data sources at configured interval (from `ConnectionConfig.pollingIntervalSeconds`)
- Runs `_search` query against configured index pattern with time-range filter (`@timestamp > LastPolledAt`)
- Fetches `_source` fields from hits, applies MessageTemplate server-side to combine selected fields into a single message string
- Auto-detects timestamp and level fields from hits (same logic as Chrome extension)
- Feeds combined entries into IngestionPipeline
- Updates `LastPolledAt` after successful poll
- On failure (connection timeout, auth error, index not found): logs error, skips this cycle, retries on next interval. No exponential backoff — keeps it simple. Frontend can see staleness via `LastPolledAt` (if it's old relative to polling interval, something is wrong).

**Field discovery** (for message template config in UI): `GET /api/datasources/{id}/fields` runs a sample `_search` (size=10) against the ES index, returns union of all `_source` field names with sample values. Frontend uses this to populate the field picker when configuring the message template.

### BaselineRecalculationService

- Runs every hour
- Recalculates PatternBaseline for all patterns using last 4 weeks of PatternOccurrence data
- Computes avg + stddev per hour-of-week slot (168 slots per pattern)

### DataRetentionService

- Runs daily
- Deletes PatternOccurrence rows older than 6 weeks (baselines use 4 weeks; 2-week buffer)
- Deletes LogPatterns with no occurrences in the last 6 weeks and IsNew=false (stale patterns)
- FK cascade deletes handle associated PatternBaseline rows when a LogPattern is deleted
- DrainState rows are cleaned up when their DataSource is deleted (FK cascade)
- Configurable retention period (default 6 weeks)

---

## Chrome Extension (v2)

Carried forward from v1, simplified.

### Architecture

- **Content script** (`kibana-interceptor.ts`) — monkey-patches `window.fetch` at `document_start`, intercepts Kibana ES proxy calls, sends captured queries to service worker
- **Service worker** (`service-worker.ts`) — manages subscriptions, scheduled polling via `chrome.alarms`, pushes hits to `/api/ingest`
- **Popup** — React 19 + MUI 7, 3 tabs

### Subscribe Flow

1. Content script captures Kibana query + response
2. User opens popup, sees captured query in "Recent Queries" tab
3. Clicks "Subscribe" — dialog shows:
   - Available fields from the captured response (checkboxes)
   - User selects fields to include in message (e.g., `service.name`, `error.type`, `message`, `stack_trace`)
   - Drag or arrows to set field order
   - Polling interval slider (0.5-60 minutes)
4. Extension creates DataSource via `POST /api/datasources` with:
   - Type: KibanaProxy
   - MessageTemplate built from selected fields
   - ConnectionConfig with query details
5. Polling starts via `chrome.alarms`

### Auto-Detection

- **Timestamp:** auto-detects `@timestamp`, `timestamp` fields from response
- **Level:** auto-detects `log.level`, `level`, `severity` fields from response
- User does not need to map these manually

### Field Selector Details

The field selector dialog in the Subscribe flow:

- **Field extraction:** Union of all `_source` field keys from the first 10 hits in the captured response. Handles sparse data (some hits may have fields others don't).
- **Display:** Checkboxes with field names, sample value shown next to each. Auto-detected timestamp/level fields shown as pre-checked and labeled.
- **Ordering:** Selected fields can be reordered via up/down arrows. Order determines position in the combined message template.
- **Preview:** Live preview of what the combined message looks like for the first hit, e.g., `"payment-service | TimeoutException | Connection to db timed out after 5000ms"`.
- **Max fields:** No hard limit, but a soft warning if more than 6 fields selected (Drain works best with concise input).

### Improvements over v1

- **Per-subscription pause/resume** instead of all-or-nothing session failure
- **Cleaner bsearch parsing** — fix v1's dedup and parsing issues
- **Simpler data model** — no schema mapping, no fingerprint config, no sampling budget
- **Field selector** — new feature, picks which ELK fields to combine

### Popup Tabs

- **Recent Queries** — captured queries list, Subscribe button with field selector dialog
- **Active Subscriptions** — status per subscription (Active/Paused), pause/resume/delete controls
- **Settings** — Log Jammer API URL, API token (optional, for future auth), save button. Token is sent as `Authorization: Bearer` header if configured; backend ignores it until auth is implemented.

---

## Frontend (v2)

React 19 + Vite + TypeScript 5.9 + MUI 7 + TanStack Query 5 + Chart.js 4.

### Pages

#### Dashboard (`/dashboard`)
- **Stats bar** — total patterns, new pattern count, ingestion rate (messages/hour)
- **New patterns section** — list of patterns with `IsNew = true`, showing template (truncated), severity, first seen, data source. "Acknowledge" button per pattern.
- **Anomalies section** — patterns sorted by sigma deviation (highest first). Shows: template, current rate, expected rate, deviation badge. Click navigates to pattern detail.

#### Data Sources (`/data-sources`)
- **Table** — name, type, enabled toggle, last polled, status indicator
- **Create/Edit dialog:**
  - Type picker: KibanaProxy or Elasticsearch
  - KibanaProxy: read-only display (configured via extension)
  - Elasticsearch: URL, index pattern, auth (optional), polling interval, message template with field picker (query sample data, show available fields, checkboxes + ordering, with auto-detect override for timestamp/level)
- **Delete** with confirmation
- **Test connection** button (Elasticsearch only)

#### Pattern Detail (`/patterns/{id}`)
- Full template string
- Sample message (real log line)
- Severity badge
- Data source name
- First seen / last seen
- **Occurrence chart** — hourly counts as line chart, with baseline expected range as shaded band overlay
- Current rate vs expected rate comparison

### Layout
- Sidebar with 3 nav items: Dashboard, Data Sources, (Patterns accessed via dashboard clicks)
- Top bar with app name
- Dark theme (monitoring aesthetic, carried from v1)

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Backend | .NET 10 / C# 13 |
| Frontend | Vite 7 + React 19 + TypeScript 5.9 + MUI 7 |
| Database | PostgreSQL 17 (no pgvector) |
| ORM | EF Core 10 |
| Chrome Extension | Vite + React 19 + TypeScript + MUI 7, Manifest V3 |
| Log Parsing | Drain algorithm (C# port) |
| Charts | Chart.js 4 |
| Server State | TanStack Query 5 |
| Routing | React Router 7 |

---

## What's Removed from v1

- ONNX Runtime / ML embeddings / all-MiniLM-L6-v2 model
- pgvector extension
- Classification queue, tags, tag centroids, user overrides
- Alert system (alert entity, spike detection rules, capped escalation, correlation detection)
- Fingerprint calculator, fingerprint aliases, fingerprint normalization heuristics
- Adaptive sampling
- Schema mapping (replaced by message template)
- 4-project solution (Core/Infrastructure/Api/Tests → Engine/Api)
- Auth system
- SampleLog TUI tool
- PostgreSQL adapter, LogFile adapter
- 12 of 15+ entities

## What's New in v2

- Drain algorithm (C# port) for reliable pattern extraction
- Stack trace preprocessing (auto top-3 frame extraction)
- Message template with field aggregation
- Hour-of-week baselines with statistical deviation
- Field selector in Chrome extension during subscribe
- Per-subscription error recovery in extension

## What's Carried Forward (Simplified)

- Chrome extension Kibana bridge (improved)
- Elasticsearch ingestion
- PostgreSQL storage
- React + MUI + TanStack Query frontend
- Docker deployment
- Modular project structure (2 projects vs 4)
