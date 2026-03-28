# Extension Config Sync — Server-Side Subscription Storage

## Overview

Store Chrome extension subscription configuration on the Log Jammer server so that switching browsers/computers only requires entering the API key — all subscriptions restore automatically.

**Core principle:** Server is the source of truth. Extension is a thin client for subscription config.

## Data Model Changes

### DataSource Entity

Add `Version` field:
- Type: `int`, default `1`
- Annotated with `[ConcurrencyCheck]`
- Incremented on every successful update
- EF Core uses this to detect concurrent modifications

Migration required: add `Version` column to `DataSources` table.

### ConnectionConfig for KibanaProxy (expanded JSONB)

Currently stores an arbitrary string. New structure for `KibanaProxy` type:

```json
{
  "kibanaUrl": "https://kibana.example.com",
  "indexPattern": "logs-*",
  "queryDsl": { "query": { "bool": { ... } } },
  "fullRequestBody": { ... },
  "selectedFields": ["service", "message", "log.level"],
  "messageTemplate": "{service} | {message}",
  "pollIntervalMinutes": 5,
  "subscriptionStatus": "active",
  "lastSubscribedAt": "2026-03-28T10:00:00Z"
}
```

Contains everything the extension needs to restore and resume a subscription:
- `queryDsl` + `fullRequestBody`: the Kibana request needed for replay
- `selectedFields` + `messageTemplate`: how to extract and format log entries
- `pollIntervalMinutes`: polling frequency
- `subscriptionStatus`: whether this subscription is actively polling
- `lastSubscribedAt`: when the subscription was created

No new tables required. Elasticsearch-type DataSources continue using ConnectionConfig as a URL string — the field is type-agnostic.

### DTO Changes

**DataSourceResponse** — add:
- `version` (int)

**UpdateDataSourceRequest** — add:
- `version` (int, required)

**CreateDataSourceRequest** — no changes (version starts at 1 server-side).

**IngestResponse** — add:
- `skipped` (bool, default false)
- `reason` (string, optional)

## API Changes

### Optimistic Concurrency on Update

`PUT /api/datasources/{id}`:

1. Request body must include `version`
2. EF Core checks version matches stored value (via `[ConcurrencyCheck]`)
3. On match: save, increment version, return `200` with updated response including new version
4. On mismatch: catch `DbUpdateConcurrencyException`, return `409 Conflict`:

```json
{
  "error": "conflict",
  "message": "DataSource was modified by another client",
  "currentVersion": 5
}
```

### Poll Interval Guard on Ingest

`POST /api/ingest/{dataSourceId}`:

Before processing entries, check timing:

```
timeSinceLastPoll = now - dataSource.LastPolledAt
pollInterval = extract pollIntervalMinutes from ConnectionConfig (for KibanaProxy)
threshold = pollInterval * 0.5

if lastPolledAt is not null AND timeSinceLastPoll < threshold:
    return 200 {
      accepted: 0,
      duplicates: 0,
      failed: 0,
      skipped: true,
      reason: "Another client polled {timeSinceLastPoll}s ago, next window in {remaining}s"
    }
```

- Not a hard rejection (200 status), just signals duplicate polling
- Only applies to KibanaProxy type DataSources
- First ingest in a window proceeds normally and updates `LastPolledAt`

### No Other Endpoint Changes

`GET /api/datasources` already returns all KibanaProxy datasources with `ConnectionConfig`. The extension reads richer JSON now — no new endpoints needed.

## Extension Sync Flow

### On Startup / Authentication

```
Extension authenticates (API key verified)
    ↓
GET /api/datasources (filter type: KibanaProxy)
    ↓
For each server datasource:
  ├── Exists locally with same dataSourceId?
  │   ├── Server version > local → update local state
  │   └── Equal → no action
  └── Not found locally → create local subscription (paused)
    ↓
Local subscriptions whose dataSourceId missing from server → remove locally
    ↓
Toast: "Synced 3 subscriptions from server (1 new, 2 updated)"
```

### On Popup Open

Same pull logic as startup. Lighter toast — only if changes detected.

### On Local Change

Any local change (create, pause, change interval, update fields):

1. Immediately `PUT /api/datasources/{id}` with current `version`
2. On `200`: update local version to returned value
3. On `409`: pull latest via GET, refresh local state, show toast "Config was updated from another session — refreshed"

### On Subscription Create

1. `POST /api/datasources` with full `ConnectionConfig`
2. Server returns `id` + `version: 1`
3. Extension stores both locally

## Extension UX

### Toast Notifications

| Scenario | Message |
|----------|---------|
| First sync on new browser | "Restored 3 subscriptions from Log Jammer server" |
| Incremental sync with changes | "1 subscription updated from server" |
| Optimistic concurrency conflict | "Config was updated from another session — refreshed" |
| Poll guard triggered | "Another instance is already polling [name]" |

### Subscriptions Tab

- Restored subscriptions arrive as **paused** — user activates the ones they want to poll from this browser
- Small indicator showing sync status (synced / out of sync)

### Settings

No changes — `logJammerUrl` and `apiKey` are entered manually per browser. They bootstrap the sync connection.

## Implementation Scope

### Backend
1. Add `Version` to DataSource entity + EF migration
2. Update DTOs (response includes version, update requires version)
3. Add concurrency handling in DataSourcesController (catch `DbUpdateConcurrencyException` → 409)
4. Add poll interval guard in IngestController
5. Add `skipped` and `reason` fields to IngestResponse

### Chrome Extension
1. On auth/startup: pull datasources, reconcile with local subscriptions
2. On every local change: push to server with version
3. Handle 409 conflicts: pull and refresh
4. Handle ingest skipped response: show toast
5. Restored subscriptions default to paused
6. Toast notification system for sync events

### Specs to Update
- `specs/definition-dto.md` — Version field, expanded ConnectionConfig, IngestResponse changes
- `specs/definition-api.md` — 409 response, poll guard behavior
