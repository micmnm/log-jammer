# Definition DTO

## Authentication

### AuthSettings
`LogJammer.Api.Auth.AuthSettings`
- `Password` (string, required)
- `ApiKey` (string, required)

Configured via the `Auth` section in `appsettings.json` or env vars (`Auth__Password`, `Auth__ApiKey`).

Two authentication methods are accepted for all `/api/*` endpoints (except `/api/auth/login`):
- `Authorization: Bearer <token>` — JWT-style token obtained from `/api/auth/login`
- `X-Api-Key: <key>` — static API key (used by the Chrome extension)

### LoginRequest
`LogJammer.Api.Dtos.LoginRequest` (record)
- `Password` (string)

### LoginResponse
`LogJammer.Api.Dtos.LoginResponse` (record)
- `Token` (string)

---

## Enums

### DataSourceType
`LogJammer.Engine.Data.Entities.DataSourceType`
- `KibanaProxy` — receive-only; log entries pushed via `/api/ingest/{id}`
- `Elasticsearch` — server-side polling via `ElasticsearchPollingService`

### Severity
`LogJammer.Engine.Data.Entities.Severity`
- `Info`
- `Warning`
- `Error`
- `Critical`

---

## Entities

### DataSource
`LogJammer.Engine.Data.Entities.DataSource` → `data_sources`

| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK, auto-generated |
| Name | string | max 200 |
| Type | DataSourceType | |
| ConnectionConfig | string | jsonb |
| MessageTemplate | string? | max 500; Serilog-style template for extracting fields |
| Enabled | bool | default true |
| CreatedAt | DateTimeOffset | auto-set |
| LastPolledAt | DateTimeOffset? | updated by polling service |
| Version | int | default 1; concurrency check token |

Navigation: `DrainState`, `Patterns` (ICollection<LogPattern>)

### DrainState
`LogJammer.Engine.Data.Entities.DrainState` → `drain_states`

Persists the serialized Drain trie between restarts so pattern history is not lost.

| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| DataSourceId | Guid | FK → data_sources (1:1) |
| SerializedState | byte[] | MessagePack-serialized Drain trie |
| UpdatedAt | DateTimeOffset | updated on each checkpoint |

Navigation: `DataSource`

### LogPattern
`LogJammer.Engine.Data.Entities.LogPattern` → `log_patterns`

| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| Template | string | max 2000; Drain-generated template with `<*>` wildcards |
| ClusterId | int | numeric cluster ID assigned by DrainParser |
| FirstSeen | DateTimeOffset | |
| LastSeen | DateTimeOffset | |
| SampleMessage | string | max 4000; most recent raw message |
| Severity | Severity | derived from log level |
| DataSourceId | Guid | FK → data_sources |
| IsNew | bool | true until acknowledged by user |

Navigation: `DataSource`, `Occurrences` (ICollection<PatternOccurrence>), `Baselines` (ICollection<PatternBaseline>)

### PatternOccurrence
`LogJammer.Engine.Data.Entities.PatternOccurrence` → `pattern_occurrences`

One row per pattern per 1-hour window. Upserted on every ingestion cycle.

| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| PatternId | Guid | FK → log_patterns |
| WindowStart | DateTimeOffset | truncated to hour |
| WindowEnd | DateTimeOffset | WindowStart + 1h |
| Count | long | cumulative count in window |

Navigation: `Pattern`

### PatternBaseline
`LogJammer.Engine.Data.Entities.PatternBaseline` → `pattern_baselines`

Statistical baseline per pattern per hour-of-week slot (0–167). Recalculated weekly from the last 4 weeks of occurrences.

| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| PatternId | Guid | FK → log_patterns |
| HourOfWeek | int | 0–167 (DayOfWeek * 24 + Hour) |
| AvgCount | double | mean occurrence count for this slot |
| StdDevCount | double | sample standard deviation |

Navigation: `Pattern`

---

## DTOs

### DataSource DTOs

#### CreateDataSourceRequest
`LogJammer.Api.Dtos.CreateDataSourceRequest` (record)
- `Name` (string)
- `Type` (DataSourceType)
- `ConnectionConfig` (string) — JSON connection config; for Elasticsearch: ES URL string; for KibanaProxy: JSON object with `kibanaUrl`, `indexPattern`, `queryDsl`, `fullRequestBody?`, `selectedFields`, `messageTemplate`, `pollIntervalMinutes`, `subscriptionStatus`, `lastSubscribedAt`
- `MessageTemplate` (string?) — optional Serilog-style message template

#### UpdateDataSourceRequest
`LogJammer.Api.Dtos.UpdateDataSourceRequest` (record)
- `Name` (string?) — null = no change
- `ConnectionConfig` (string?) — null = no change
- `MessageTemplate` (string?) — null = no change
- `Enabled` (bool?) — null = no change
- `Version` (int) — required; must match server version for optimistic concurrency

#### DataSourceResponse
`LogJammer.Api.Dtos.DataSourceResponse` (record)
- `Id` (Guid)
- `Name` (string)
- `Type` (DataSourceType)
- `ConnectionConfig` (string)
- `MessageTemplate` (string?)
- `Enabled` (bool)
- `CreatedAt` (DateTimeOffset)
- `LastPolledAt` (DateTimeOffset?)
- `Version` (int)

#### FieldInfo
`LogJammer.Api.Dtos.FieldInfo` (record)
- `Name` (string)
- `SampleValue` (string?)

---

### Ingest DTOs

#### IngestRequest
`LogJammer.Api.Dtos.IngestRequest` (record)
- `Entries` (IngestEntry[]) — max 10 000 entries per request

#### IngestEntry
`LogJammer.Api.Dtos.IngestEntry` (record)
- `Message` (string)
- `Timestamp` (DateTimeOffset)
- `Level` (string?) — e.g. "info", "error", "warn"

#### IngestResponse
`LogJammer.Api.Dtos.IngestResponse` (record)
- `Accepted` (int) — number of entries passed to the ingestion pipeline
- `Skipped` (bool) — true if rejected by poll interval guard (default false)
- `Reason` (string?) — explanation when skipped (e.g., "Another client polled 30s ago")

---

### Pattern DTOs

#### PatternListItem
`LogJammer.Api.Dtos.PatternListItem` (record)
- `Id` (Guid)
- `Template` (string)
- `Severity` (Severity)
- `FirstSeen` (DateTimeOffset)
- `LastSeen` (DateTimeOffset)
- `IsNew` (bool)
- `CurrentRate` (long) — occurrences in current hour window
- `ExpectedRate` (double) — baseline avg for this hour-of-week
- `StdDevsFromMean` (double) — anomaly score; positive = above baseline
- `DataSourceName` (string)

#### PatternDetailResponse
`LogJammer.Api.Dtos.PatternDetailResponse` (record)

All fields from `PatternListItem`, plus:
- `SampleMessage` (string)
- `Occurrences` (IEnumerable<OccurrencePoint>) — last 168 hours
- `BaselineBands` (IEnumerable<BaselineBand>) — all 168 hour-of-week slots

#### OccurrencePoint
`LogJammer.Api.Dtos.OccurrencePoint` (record)
- `WindowStart` (DateTimeOffset)
- `Count` (long)

#### BaselineBand
`LogJammer.Api.Dtos.BaselineBand` (record)
- `HourOfWeek` (int)
- `AvgCount` (double)
- `StdDevCount` (double)

#### PagedResult<T>
`LogJammer.Api.Dtos.PagedResult<T>` (record)
- `Items` (IEnumerable<T>)
- `TotalCount` (int)
- `Page` (int)
- `PageSize` (int)

---

### Dashboard DTOs

#### DashboardResponse
`LogJammer.Api.Dtos.DashboardResponse` (record)
- `TotalPatterns` (int)
- `NewPatternCount` (int)
- `IngestionRatePerHour` (long) — sum of all occurrence counts in the current hour window
- `TopAnomalies` (IEnumerable<AnomalyItem>) — up to 10, ordered by |StdDevsFromMean| desc
- `NewPatterns` (IEnumerable<NewPatternItem>) — up to 50, most recent first

#### AnomalyItem
`LogJammer.Api.Dtos.AnomalyItem` (record)
- `PatternId` (Guid)
- `Template` (string)
- `Severity` (Severity)
- `CurrentRate` (long)
- `ExpectedRate` (double)
- `StdDevsFromMean` (double)
- `DataSourceName` (string)

#### NewPatternItem
`LogJammer.Api.Dtos.NewPatternItem` (record)
- `PatternId` (Guid)
- `Template` (string)
- `Severity` (Severity)
- `FirstSeen` (DateTimeOffset)
- `DataSourceName` (string)

---

## Engine Types

### DrainConfig
`LogJammer.Engine.Drain.DrainConfig`
- `SimilarityThreshold` (double) — default 0.4; controls how aggressively messages are merged into an existing cluster
- `MaxClusters` (int) — default 1000; cap on distinct patterns per data source
- `TreeDepth` (int) — default 4; prefix tree depth for the Drain algorithm

### DrainResult
`LogJammer.Engine.Drain.DrainResult` (record)
- `ClusterId` (int)
- `Template` (string) — pattern with `<*>` wildcards
- `IsNewCluster` (bool)

### RawLogEntry
`LogJammer.Engine.Processing.RawLogEntry`
- `Message` (string, required)
- `Timestamp` (DateTimeOffset)
- `Level` (string?)
- `Fields` (Dictionary<string, string>?) — additional structured fields extracted from the message template

### BaselineComparison
`LogJammer.Engine.BaselineComparison` (record)
- `CurrentRate` (long) — occurrence count in the current hour window
- `ExpectedRate` (double) — baseline average for this hour-of-week slot
- `StdDevsFromMean` (double) — 0 when no baseline or stddev is zero
