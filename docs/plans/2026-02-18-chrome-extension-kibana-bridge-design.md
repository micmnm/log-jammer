# Chrome Extension: Kibana Bridge for Log Jammer

**Date:** 2026-02-18
**Status:** Approved

## Problem

In environments where direct Elasticsearch/Kibana API access is restricted by security policies, Log Jammer cannot connect to ELK as a data source. However, users have browser access to Kibana Discover through their authenticated sessions.

## Solution

A Chrome extension (Manifest V3) that acts as a bridge between Kibana and Log Jammer. The extension intercepts Kibana Discover queries, lets the user select which queries to monitor, then periodically re-runs them through Kibana's authenticated proxy and pushes results to Log Jammer.

## Architecture: Extension-Driven Push

```
Kibana Discover ──► Content Script (captures ES queries via fetch interception)
                          │
                          ▼
                    Extension Popup (shows recent queries, manage subscriptions)
                          │  user selects query + sets poll interval
                          ▼
                    Service Worker (chrome.alarms scheduler)
                          │  on each tick:
                          │  1. Re-run query via Kibana proxy (session cookies)
                          │  2. POST results to Log Jammer ingest endpoint
                          ▼
                    Log Jammer API ──► SchemaMapper ──► Fingerprint ──► Classify
```

## Chrome Extension

### Manifest V3

- **Content Script**: Injected into Kibana pages. Monkey-patches `window.fetch` to intercept ES API calls (`_search`, `_msearch`, `/internal/search/es`, `/internal/bsearch`). Extracts query DSL, index pattern, and metadata. Sends to service worker via `chrome.runtime.sendMessage`.

- **Service Worker** (background): Stores captured queries in `chrome.storage.local`. Manages subscribed queries and poll intervals via `chrome.alarms` (minimum 1-minute interval). On each alarm: adjusts query time range to `[lastPollTimestamp, now]` for incremental ingestion, fetches via captured Kibana proxy URL (browser cookies authenticate), maps ES response `hits.hits[]._source` to ingest format, POSTs to Log Jammer `POST /api/ingest/{dataSourceId}`.

- **Popup UI** (React + MUI, consistent with Log Jammer frontend):
  - Recent Queries tab: shows captured queries with human-readable summaries
  - Active Subscriptions tab: shows running subscriptions with status (last poll, last error, session health)
  - Settings: Log Jammer instance URL (localhost or external), optional API key
  - Session status indicator: green/yellow/red for Kibana session health

- **host_permissions**: Configured for Kibana host + Log Jammer URL. Service worker bypasses CORS entirely — no backend CORS changes needed.

### Session Expiry Handling

- If Kibana fetch returns 401/403, the extension pauses that subscription's alarm and shows a badge notification ("Session expired")
- On next Kibana page visit (content script fires), detects active session and resumes all paused subscriptions

### Query Capture Logic

1. Content script wraps `window.fetch` before Kibana's JS runs
2. Filters for URL patterns matching Kibana's ES proxy endpoints
3. Parses request body to extract query DSL
4. Generates human-readable summary (e.g., "logs-* | level:ERROR AND service:api-gateway | last 15m")
5. Sends `{ url, method, queryDsl, indexPattern, summary, capturedAt }` to service worker
6. Service worker deduplicates (same query DSL = same entry) and stores

## Backend Changes

### New AdapterType: KibanaProxy

Added to `AdapterType` enum. Receive-only adapter — does not pull data.

- `PollErrorsAsync` → `NotSupportedException` (polling done by extension)
- `TestConnectionAsync` → no-op success
- `GetSchemaAsync` / `GetSampleRecordsAsync` → informational only

ConnectionConfig stores metadata:
```json
{
  "kibanaUrl": "https://kibana.corp.com",
  "indexPattern": "logs-*",
  "queryDsl": { },
  "capturedAt": "2026-02-18T10:30:00Z"
}
```

`DataSourcePollingManager` skips KibanaProxy sources (no server-side polling).

### New Ingest Endpoint

```
POST /api/ingest/{dataSourceId}
Content-Type: application/json

{
  "entries": [
    {
      "timestamp": "2026-02-18T10:30:00Z",
      "fields": { /* raw key-value pairs from ES hit._source */ }
    }
  ]
}

Response: { "accepted": 150, "duplicates": 12 }
```

Processing pipeline (reuses existing infrastructure):
1. Validate DataSource exists and is type KibanaProxy
2. For each entry: SchemaMapper.Map() → FingerprintCalculator.ComputeFingerprint()
3. KnownError upsert (by fingerprint hash, then alias lookup)
4. ErrorOccurrence window upsert (5-minute buckets)
5. ClassificationQueue enqueue (for new KnownErrors)

### Schema Mapping

Same as other adapters — user configures field mapping (ES field paths → Log Jammer standard fields) via the existing Schema Mapping dialog in the frontend, or from the extension popup which calls `PUT /api/datasources/{id}`.

## Project Structure

```
src/chrome-extension/
├── manifest.json
├── package.json
├── tsconfig.json
├── vite.config.ts
├── src/
│   ├── content/
│   │   └── kibana-interceptor.ts
│   ├── background/
│   │   └── service-worker.ts
│   ├── popup/
│   │   ├── popup.html
│   │   ├── popup.tsx
│   │   └── components/
│   │       ├── RecentQueries.tsx
│   │       ├── ActiveSubscriptions.tsx
│   │       └── Settings.tsx
│   ├── shared/
│   │   ├── types.ts
│   │   └── kibana-query-parser.ts
│   └── utils/
│       └── storage.ts
├── icons/
└── tests/
```

Build: Vite with separate entry points for content script, service worker, and popup. `npm run build` produces unpacked extension directory.

## Testing Strategy

- **Unit tests (Vitest):** Query parser (ES DSL → human-readable summary), data mapper (ES hits → ingest format), time range adjuster for incremental polling
- **Backend integration tests:** New `/api/ingest` endpoint with Testcontainers — push sample data, verify KnownErrors, ErrorOccurrences, and ClassificationQueue entries created correctly
- **Manual testing:** Load unpacked extension in Chrome, open Kibana, verify query capture, subscribe, verify data flows into Log Jammer

## Key Design Decisions

1. **Extension-driven push** over WebSocket bridge or manual export — simplest architecture, self-contained scheduling
2. **MUI in popup** for visual consistency with Log Jammer frontend (accepts ~200KB bundle cost)
3. **Incremental time range** adjustment prevents duplicate data ingestion
4. **No backend CORS changes** — service worker bypasses CORS via host_permissions
5. **One subscribed query = one DataSource** — leverages existing per-source fingerprinting, classification, and error grouping
