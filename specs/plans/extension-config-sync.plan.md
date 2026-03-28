# Extension Config Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Store Chrome extension subscription config on the Log Jammer server so switching browsers only requires an API key to restore all subscriptions.

**Architecture:** Server is source of truth. DataSource.ConnectionConfig (JSONB) stores the full subscription config for KibanaProxy type. Optimistic concurrency via a Version field prevents silent overwrites. Poll interval guard on ingest prevents duplicate data from multiple browser instances.

**Tech Stack:** .NET 10 / C# 13, EF Core 10, PostgreSQL 17, Chrome Extension (TypeScript, Vite, React 19, MUI 7)

**Agent Assignment:**
- Tasks 1-4: Backend agent (sonnet)
- Tasks 5-8: Frontend/extension agent (sonnet)
- Task 9: Test agent (haiku)
- Task 10: Backend agent (sonnet)

---

### Task 1: Add Version field to DataSource entity + migration

**Files:**
- Modify: `src/LogJammer.Engine/Data/Entities/DataSource.cs`
- Modify: `src/LogJammer.Engine/Data/LogJammerDbContext.cs`
- Create: `src/LogJammer.Engine/Data/Migrations/<timestamp>_AddDataSourceVersion.cs` (via EF tooling)

- [ ] **Step 1: Add Version property to DataSource entity**

In `src/LogJammer.Engine/Data/Entities/DataSource.cs`, add the Version property with ConcurrencyCheck:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogJammer.Engine.Data.Entities;

public class DataSource
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public DataSourceType Type { get; set; }

    [Column(TypeName = "jsonb")]
    public required string ConnectionConfig { get; set; }

    [MaxLength(500)]
    public string? MessageTemplate { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastPolledAt { get; set; }

    [ConcurrencyCheck]
    public int Version { get; set; } = 1;

    public DrainState? DrainState { get; set; }
    public ICollection<LogPattern> Patterns { get; set; } = [];
}
```

- [ ] **Step 2: Generate EF Core migration**

Run:
```bash
dotnet ef migrations add AddDataSourceVersion --project src/LogJammer.Engine --startup-project src/LogJammer.Api
```

Expected: Migration file created in `src/LogJammer.Engine/Data/Migrations/`

- [ ] **Step 3: Verify migration applies cleanly**

Run:
```bash
dotnet ef database update --project src/LogJammer.Engine --startup-project src/LogJammer.Api
```

Expected: Migration applied, `Version` column added to `data_sources` table with default value 1.

- [ ] **Step 4: Build to verify no errors**

Run:
```bash
dotnet build src/LogJammer.slnx
```

Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/LogJammer.Engine/Data/Entities/DataSource.cs src/LogJammer.Engine/Data/Migrations/
git commit -m "feat: add Version concurrency field to DataSource entity"
```

---

### Task 2: Update DataSource DTOs to include Version

**Files:**
- Modify: `src/LogJammer.Api/Dtos/DataSourceDtos.cs`

- [ ] **Step 1: Update DTOs**

Replace the content of `src/LogJammer.Api/Dtos/DataSourceDtos.cs`:

```csharp
using LogJammer.Engine.Data.Entities;

namespace LogJammer.Api.Dtos;

public record CreateDataSourceRequest(
    string Name,
    DataSourceType Type,
    string ConnectionConfig,
    string? MessageTemplate);

public record UpdateDataSourceRequest(
    string? Name,
    string? ConnectionConfig,
    string? MessageTemplate,
    bool? Enabled,
    int Version);

public record DataSourceResponse(
    Guid Id,
    string Name,
    DataSourceType Type,
    string ConnectionConfig,
    string? MessageTemplate,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastPolledAt,
    int Version);

public record FieldInfo(string Name, string? SampleValue);
```

Key changes:
- `UpdateDataSourceRequest`: added `Version` (int, required)
- `DataSourceResponse`: added `Version` (int)

- [ ] **Step 2: Build to verify**

Run:
```bash
dotnet build src/LogJammer.slnx
```

Expected: Build will fail — `DataSourcesController.cs` references to `ToResponse()` and `Update()` need updating. That's Task 3.

- [ ] **Step 3: Commit**

```bash
git add src/LogJammer.Api/Dtos/DataSourceDtos.cs
git commit -m "feat: add Version to DataSource DTOs"
```

---

### Task 3: Update DataSourcesController with concurrency handling

**Files:**
- Modify: `src/LogJammer.Api/Controllers/DataSourcesController.cs`

- [ ] **Step 1: Update ToResponse to include Version**

In `src/LogJammer.Api/Controllers/DataSourcesController.cs`, update the `ToResponse` helper at line 113:

```csharp
private static DataSourceResponse ToResponse(DataSource source) => new(
    source.Id,
    source.Name,
    source.Type,
    source.ConnectionConfig,
    source.MessageTemplate,
    source.Enabled,
    source.CreatedAt,
    source.LastPolledAt,
    source.Version);
```

- [ ] **Step 2: Update the Update method with concurrency handling**

Replace the `Update` method (lines 56-73) with:

```csharp
[HttpPut("{id:guid}")]
public async Task<ActionResult<DataSourceResponse>> Update(Guid id, [FromBody] UpdateDataSourceRequest request)
{
    var source = await db.DataSources.FirstOrDefaultAsync(d => d.Id == id);
    if (source is null)
        return NotFound();

    if (request.Version != source.Version)
        return Conflict(new { error = "conflict", message = "DataSource was modified by another client", currentVersion = source.Version });

    if (request.Name is not null)
        source.Name = request.Name;
    if (request.ConnectionConfig is not null)
        source.ConnectionConfig = request.ConnectionConfig;
    if (request.MessageTemplate is not null)
        source.MessageTemplate = request.MessageTemplate;
    if (request.Enabled.HasValue)
        source.Enabled = request.Enabled.Value;

    source.Version++;

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Conflict(new { error = "conflict", message = "DataSource was modified by another client", currentVersion = source.Version });
    }

    return Ok(ToResponse(source));
}
```

- [ ] **Step 3: Build to verify**

Run:
```bash
dotnet build src/LogJammer.slnx
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/LogJammer.Api/Controllers/DataSourcesController.cs
git commit -m "feat: add optimistic concurrency to DataSource updates"
```

---

### Task 4: Add poll interval guard to IngestController

**Files:**
- Modify: `src/LogJammer.Api/Dtos/IngestDtos.cs`
- Modify: `src/LogJammer.Api/Controllers/IngestController.cs`

- [ ] **Step 1: Update IngestResponse DTO**

Replace `src/LogJammer.Api/Dtos/IngestDtos.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace LogJammer.Api.Dtos;

public record IngestRequest(
    [MaxLength(10000)] IngestEntry[] Entries);

public record IngestEntry(
    string Message,
    DateTimeOffset Timestamp,
    string? Level);

public record IngestResponse(int Accepted, bool Skipped = false, string? Reason = null);
```

- [ ] **Step 2: Add poll interval guard to IngestController**

Replace `src/LogJammer.Api/Controllers/IngestController.cs`:

```csharp
using System.Text.Json;
using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Processing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/ingest")]
public class IngestController(LogJammerDbContext db, IngestionPipeline pipeline) : ControllerBase
{
    [HttpPost("{dataSourceId:guid}")]
    public async Task<ActionResult<IngestResponse>> Ingest(Guid dataSourceId, [FromBody] IngestRequest request)
    {
        var source = await db.DataSources.FirstOrDefaultAsync(d => d.Id == dataSourceId);
        if (source is null)
            return NotFound(new { message = "Data source not found" });

        if (!source.Enabled)
            return BadRequest(new { message = "Data source is disabled" });

        // Poll interval guard: reject if another client polled too recently
        if (source.Type == DataSourceType.KibanaProxy && source.LastPolledAt.HasValue)
        {
            var pollIntervalMinutes = ExtractPollIntervalMinutes(source.ConnectionConfig);
            if (pollIntervalMinutes.HasValue)
            {
                var timeSinceLastPoll = DateTimeOffset.UtcNow - source.LastPolledAt.Value;
                var threshold = TimeSpan.FromMinutes(pollIntervalMinutes.Value * 0.5);
                if (timeSinceLastPoll < threshold)
                {
                    var remaining = threshold - timeSinceLastPoll;
                    return Ok(new IngestResponse(
                        Accepted: 0,
                        Skipped: true,
                        Reason: $"Another client polled {timeSinceLastPoll.TotalSeconds:F0}s ago, next window in {remaining.TotalSeconds:F0}s"));
                }
            }
        }

        var entries = request.Entries.Select(e => new RawLogEntry
        {
            Message = e.Message,
            Timestamp = e.Timestamp,
            Level = e.Level,
        }).ToList();

        await pipeline.ProcessEntriesAsync(entries, dataSourceId, source.MessageTemplate);

        // Update LastPolledAt
        source.LastPolledAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new IngestResponse(entries.Count));
    }

    private static double? ExtractPollIntervalMinutes(string connectionConfig)
    {
        try
        {
            using var doc = JsonDocument.Parse(connectionConfig);
            if (doc.RootElement.TryGetProperty("pollIntervalMinutes", out var prop))
                return prop.GetDouble();
        }
        catch (JsonException)
        {
            // ConnectionConfig is not JSON (e.g., plain URL for Elasticsearch) — no poll interval
        }
        return null;
    }
}
```

- [ ] **Step 3: Build to verify**

Run:
```bash
dotnet build src/LogJammer.slnx
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/LogJammer.Api/Dtos/IngestDtos.cs src/LogJammer.Api/Controllers/IngestController.cs
git commit -m "feat: add poll interval guard to prevent duplicate ingestion"
```

---

### Task 5: Update extension types for sync support

**Files:**
- Modify: `src/chrome-extension/src/shared/types.ts`

- [ ] **Step 1: Update types**

Replace `src/chrome-extension/src/shared/types.ts`:

```typescript
export interface CapturedQuery {
  id: string;
  kibanaUrl: string;
  proxyEndpoint: string;
  method: string;
  indexPattern: string;
  queryDsl: Record<string, unknown>;
  /** Full bsearch request body (batch wrapper included) for replay */
  fullRequestBody?: Record<string, unknown>;
  summary: string;
  capturedAt: string;
  /** Sample fields extracted from the first response hits */
  sampleFields?: { name: string; sampleValue: string }[];
}

export interface Subscription {
  id: string;
  queryId: string;
  dataSourceId: string;
  name: string;
  pollIntervalMinutes: number;
  lastPollAt: string | null;
  lastError: string | null;
  status: 'active' | 'paused' | 'error';
  /** Fields selected for the message template */
  selectedFields: string[];
  /** Message template built from selected fields, e.g. "{service} | {message}" */
  messageTemplate: string;
  /** Snapshot of the captured query at subscription time — immune to query rotation */
  querySnapshot?: CapturedQuery;
  /** Server-side version for optimistic concurrency */
  version: number;
}

export interface ExtensionSettings {
  logJammerUrl: string;
  apiKey: string;
  maxCapturedQueries: number;
  defaultPollIntervalMinutes: number;
  verbose: boolean;
  errorDetails: boolean;
}

export const DEFAULT_SETTINGS: ExtensionSettings = {
  logJammerUrl: 'http://localhost:5050',
  apiKey: '',
  maxCapturedQueries: 50,
  defaultPollIntervalMinutes: 5,
  verbose: false,
  errorDetails: false,
};

export interface IngestEntry {
  message: string;
  timestamp: string;
  level?: string;
}

export interface IngestResponse {
  accepted: number;
  duplicates: number;
  failed: number;
  skipped?: boolean;
  reason?: string;
}

/** Shape of DataSourceResponse from the Log Jammer API */
export interface DataSourceResponse {
  id: string;
  name: string;
  type: string;
  connectionConfig: string;
  messageTemplate: string | null;
  enabled: boolean;
  createdAt: string;
  lastPolledAt: string | null;
  version: number;
}

/** KibanaProxy ConnectionConfig stored server-side */
export interface KibanaProxyConfig {
  kibanaUrl: string;
  indexPattern: string;
  queryDsl: Record<string, unknown>;
  fullRequestBody?: Record<string, unknown>;
  selectedFields: string[];
  messageTemplate: string;
  pollIntervalMinutes: number;
  subscriptionStatus: 'active' | 'paused';
  lastSubscribedAt: string;
}

/** Sync status toast notification */
export interface SyncResult {
  restored: number;
  updated: number;
  removed: number;
}
```

- [ ] **Step 2: Build extension to verify types compile**

Run:
```bash
cd src/chrome-extension && npx tsc --noEmit
```

Expected: Type errors — service-worker.ts and other files need updating. Those are subsequent tasks.

- [ ] **Step 3: Commit**

```bash
git add src/chrome-extension/src/shared/types.ts
git commit -m "feat: add sync types to chrome extension"
```

---

### Task 6: Add sync logic to service worker

**Files:**
- Modify: `src/chrome-extension/src/background/service-worker.ts`

- [ ] **Step 1: Add sync functions to service-worker.ts**

Add the following sync functions before the `restoreAlarms()` call at the end of the file (before line 624):

```typescript
// --- Server sync ---

async function buildApiHeaders(): Promise<Record<string, string>> {
  const settings = await StorageManager.getSettings();
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (settings.apiKey) headers['X-Api-Key'] = settings.apiKey;
  return headers;
}

async function getApiUrl(): Promise<string> {
  const settings = await StorageManager.getSettings();
  return settings.logJammerUrl;
}

function buildConnectionConfig(subscription: Subscription): string {
  const query = subscription.querySnapshot;
  const config: KibanaProxyConfig = {
    kibanaUrl: query?.kibanaUrl ?? '',
    indexPattern: query?.indexPattern ?? '',
    queryDsl: query?.queryDsl ?? {},
    fullRequestBody: query?.fullRequestBody,
    selectedFields: subscription.selectedFields,
    messageTemplate: subscription.messageTemplate,
    pollIntervalMinutes: subscription.pollIntervalMinutes,
    subscriptionStatus: subscription.status === 'error' ? 'paused' : subscription.status,
    lastSubscribedAt: new Date().toISOString(),
  };
  return JSON.stringify(config);
}

function subscriptionFromDataSource(ds: DataSourceResponse): Subscription | null {
  if (ds.type !== 'KibanaProxy') return null;

  let config: KibanaProxyConfig;
  try {
    config = JSON.parse(ds.connectionConfig) as KibanaProxyConfig;
  } catch {
    return null;
  }

  // Skip if connectionConfig doesn't have the new sync fields
  if (!config.selectedFields || !config.messageTemplate) return null;

  const querySnapshot: CapturedQuery = {
    id: crypto.randomUUID(),
    kibanaUrl: config.kibanaUrl,
    proxyEndpoint: '',
    method: 'POST',
    indexPattern: config.indexPattern,
    queryDsl: config.queryDsl,
    fullRequestBody: config.fullRequestBody,
    summary: `Synced from server`,
    capturedAt: config.lastSubscribedAt,
  };

  return {
    id: crypto.randomUUID(),
    queryId: querySnapshot.id,
    dataSourceId: ds.id,
    name: ds.name,
    pollIntervalMinutes: config.pollIntervalMinutes,
    lastPollAt: ds.lastPolledAt,
    lastError: null,
    status: 'paused', // Restored subscriptions start paused
    selectedFields: config.selectedFields,
    messageTemplate: config.messageTemplate,
    querySnapshot,
    version: ds.version,
  };
}

async function syncFromServer(): Promise<SyncResult> {
  const result: SyncResult = { restored: 0, updated: 0, removed: 0 };
  const apiUrl = await getApiUrl();
  const headers = await buildApiHeaders();

  let serverDataSources: DataSourceResponse[];
  try {
    const response = await fetch(`${apiUrl}/api/datasources`, { headers });
    if (!response.ok) {
      log(`Sync failed — server returned ${response.status}`);
      return result;
    }
    serverDataSources = (await response.json()) as DataSourceResponse[];
  } catch (err) {
    log('Sync failed — network error:', err);
    return result;
  }

  const kibanaProxySources = serverDataSources.filter(ds => ds.type === 'KibanaProxy');
  const localSubscriptions = await StorageManager.getSubscriptions();

  const serverIdSet = new Set(kibanaProxySources.map(ds => ds.id));
  const localByDataSourceId = new Map(localSubscriptions.map(s => [s.dataSourceId, s]));

  // Update or create from server
  for (const ds of kibanaProxySources) {
    const local = localByDataSourceId.get(ds.id);

    if (local) {
      // Exists locally — check if server has newer version
      if (ds.version > (local.version ?? 0)) {
        const updated = subscriptionFromDataSource(ds);
        if (updated) {
          // Preserve local-only state
          updated.id = local.id;
          updated.queryId = local.queryId;
          updated.status = local.status;
          updated.lastError = local.lastError;
          updated.lastPollAt = local.lastPollAt;
          await StorageManager.saveSubscription(updated);
          result.updated++;
          log(`Sync: updated "${ds.name}" to version ${ds.version}`);
        }
      }
    } else {
      // Not found locally — restore from server
      const restored = subscriptionFromDataSource(ds);
      if (restored) {
        await StorageManager.saveSubscription(restored);
        result.restored++;
        log(`Sync: restored "${ds.name}" (paused)`);
      }
    }
  }

  // Remove local subscriptions whose dataSourceId is missing from server
  for (const local of localSubscriptions) {
    if (!serverIdSet.has(local.dataSourceId)) {
      chrome.alarms.clear(`poll_${local.id}`);
      await clearSeenDocIds(local.id);
      await StorageManager.removeSubscription(local.id);
      result.removed++;
      log(`Sync: removed "${local.name}" (deleted on server)`);
    }
  }

  return result;
}

async function pushSubscriptionToServer(subscription: Subscription): Promise<number | null> {
  const apiUrl = await getApiUrl();
  const headers = await buildApiHeaders();
  const connectionConfig = buildConnectionConfig(subscription);

  try {
    const response = await fetch(`${apiUrl}/api/datasources/${subscription.dataSourceId}`, {
      method: 'PUT',
      headers,
      body: JSON.stringify({
        connectionConfig,
        messageTemplate: subscription.messageTemplate,
        version: subscription.version ?? 1,
      }),
    });

    if (response.status === 409) {
      log(`Sync conflict for "${subscription.name}" — pulling latest`);
      await syncFromServer();
      return null;
    }

    if (response.ok) {
      const updated = (await response.json()) as DataSourceResponse;
      return updated.version;
    }

    log(`Push failed for "${subscription.name}" — ${response.status}`);
    return null;
  } catch (err) {
    log('Push failed — network error:', err);
    return null;
  }
}
```

- [ ] **Step 2: Add import for new types at the top of service-worker.ts**

Update the import at line 4 to include the new types:

```typescript
import type { CapturedQuery, Subscription, IngestEntry, IngestResponse, DataSourceResponse, KibanaProxyConfig, SyncResult } from '../shared/types';
```

- [ ] **Step 3: Update handleSubscribe to store version and push full config**

In the `handleSubscribe` function, update the DataSource creation payload (around line 133) to send the full ConnectionConfig:

Replace the `connectionConfig` value in the `JSON.stringify` call (lines 136-141):
```typescript
connectionConfig: JSON.stringify({
  kibanaUrl: query.kibanaUrl,
  indexPattern: query.indexPattern,
  queryDsl: query.queryDsl,
  fullRequestBody: query.fullRequestBody,
  selectedFields: payload.selectedFields,
  messageTemplate,
  pollIntervalMinutes,
  subscriptionStatus: 'active',
  lastSubscribedAt: new Date().toISOString(),
} satisfies KibanaProxyConfig),
```

Update the subscription creation (around line 155) to include `version`:
```typescript
const subscription: Subscription = {
  id: crypto.randomUUID(),
  queryId: query.id,
  dataSourceId: dataSource.id,
  name: payload.name,
  pollIntervalMinutes,
  lastPollAt: null,
  lastError: null,
  status: 'active',
  selectedFields,
  messageTemplate,
  querySnapshot: query,
  version: (dataSource as DataSourceResponse).version ?? 1,
};
```

- [ ] **Step 4: Update handlePauseSubscription and handleResumeSubscription to push changes**

After `await StorageManager.saveSubscription(sub);` in `handlePauseSubscription` (line 217), add:
```typescript
const newVersion = await pushSubscriptionToServer(sub);
if (newVersion !== null) {
  sub.version = newVersion;
  await StorageManager.saveSubscription(sub);
}
```

After `await StorageManager.saveSubscription(sub);` in `handleResumeSubscription` (line 228), add:
```typescript
const newVersion = await pushSubscriptionToServer(sub);
if (newVersion !== null) {
  sub.version = newVersion;
  await StorageManager.saveSubscription(sub);
}
```

- [ ] **Step 5: Handle ingest skipped response**

In the `executePoll` function, after the `ingestResponse.ok` check (around line 455), update the success branch to handle the `skipped` field:

```typescript
} else {
  const result = await ingestResponse.json() as IngestResponse;
  if (result.skipped) {
    log(`Poll "${subscription.name}" skipped — ${result.reason}`);
    // Don't update lastPollAt — another client is handling this
    await StorageManager.saveSubscription(subscription);
    return;
  }
  subscription.lastError = null;
  log(`Poll "${subscription.name}" complete — ${result.accepted} new entries`);
}
```

- [ ] **Step 6: Add sync message handler and update startup**

Add a new message handler in the `chrome.runtime.onMessage.addListener` block (after the `KIBANA_SESSION_ACTIVE` handler, around line 65):

```typescript
if (message.type === 'SYNC_FROM_SERVER') {
  syncFromServer().then(result => sendResponse(result));
  return true;
}
```

Update `restoreAlarms()` at the bottom of the file to also sync on startup:

```typescript
async function restoreAlarms(): Promise<void> {
  const subscriptions = await StorageManager.getSubscriptions();
  for (const sub of subscriptions) {
    if (sub.status === 'active') {
      chrome.alarms.create(`poll_${sub.id}`, {
        periodInMinutes: sub.pollIntervalMinutes,
        delayInMinutes: 1,
      });
      log(`Restored alarm for "${sub.name}" (every ${sub.pollIntervalMinutes}m)`);
    }
  }

  // Sync from server on startup
  const settings = await StorageManager.getSettings();
  if (settings.apiKey) {
    const result = await syncFromServer();
    if (result.restored + result.updated + result.removed > 0) {
      log(`Startup sync: ${result.restored} restored, ${result.updated} updated, ${result.removed} removed`);
    }
  }
}

restoreAlarms();
```

- [ ] **Step 7: Build extension to verify**

Run:
```bash
cd src/chrome-extension && npx tsc --noEmit
```

Expected: No errors.

- [ ] **Step 8: Commit**

```bash
git add src/chrome-extension/src/background/service-worker.ts
git commit -m "feat: add server sync logic to extension service worker"
```

---

### Task 7: Add sync UI to extension popup

**Files:**
- Modify: `src/chrome-extension/src/popup/App.tsx`
- Modify: `src/chrome-extension/src/popup/ActiveSubscriptions.tsx`

- [ ] **Step 1: Add sync trigger and toast to App.tsx**

In `src/chrome-extension/src/popup/App.tsx`, add sync-on-open and toast notification. Add a `syncMessage` state and trigger sync when the popup opens:

Add state after existing state declarations:
```typescript
const [syncMessage, setSyncMessage] = useState<string | null>(null);
```

Add a `useEffect` that triggers sync when popup opens (after the existing `refreshState` useEffect):
```typescript
useEffect(() => {
  chrome.runtime.sendMessage({ type: 'SYNC_FROM_SERVER' }, (result) => {
    if (!result) return;
    const parts: string[] = [];
    if (result.restored > 0) parts.push(`${result.restored} restored`);
    if (result.updated > 0) parts.push(`${result.updated} updated`);
    if (result.removed > 0) parts.push(`${result.removed} removed`);
    if (parts.length > 0) {
      setSyncMessage(`Synced: ${parts.join(', ')}`);
      refreshState();
    }
  });
}, []);
```

Add a Snackbar/Alert for the sync toast (before the closing `</Box>` or `</ThemeProvider>`):
```tsx
<Snackbar
  open={syncMessage !== null}
  autoHideDuration={4000}
  onClose={() => setSyncMessage(null)}
  anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
>
  <Alert severity="info" onClose={() => setSyncMessage(null)} sx={{ width: '100%' }}>
    {syncMessage}
  </Alert>
</Snackbar>
```

Add the necessary MUI imports:
```typescript
import { Snackbar, Alert } from '@mui/material';
```

- [ ] **Step 2: Show sync indicator in ActiveSubscriptions.tsx**

In `src/chrome-extension/src/popup/ActiveSubscriptions.tsx`, add a visual indicator showing the version number for each subscription. In the card content where status is displayed, add:

```tsx
<Typography variant="caption" color="text.secondary">
  v{sub.version ?? '?'}
</Typography>
```

- [ ] **Step 3: Build and verify**

Run:
```bash
cd src/chrome-extension && npx tsc --noEmit && npm run build
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/chrome-extension/src/popup/App.tsx src/chrome-extension/src/popup/ActiveSubscriptions.tsx
git commit -m "feat: add sync toast and version indicator to extension popup"
```

---

### Task 8: Push config changes on poll interval update

**Files:**
- Modify: `src/chrome-extension/src/popup/ActiveSubscriptions.tsx`
- Modify: `src/chrome-extension/src/background/service-worker.ts`

- [ ] **Step 1: Add UPDATE_POLL_INTERVAL message handler to service worker**

In `src/chrome-extension/src/background/service-worker.ts`, add a new message handler in the listener block:

```typescript
if (message.type === 'UPDATE_POLL_INTERVAL') {
  handleUpdatePollInterval(message.payload).then(result => sendResponse(result));
  return true;
}
```

Add the handler function:

```typescript
async function handleUpdatePollInterval(payload: {
  subscriptionId: string;
  pollIntervalMinutes: number;
}): Promise<{ ok: boolean; error?: string }> {
  const subscriptions = await StorageManager.getSubscriptions();
  const sub = subscriptions.find(s => s.id === payload.subscriptionId);
  if (!sub) return { ok: false, error: 'Subscription not found' };

  sub.pollIntervalMinutes = payload.pollIntervalMinutes;
  await StorageManager.saveSubscription(sub);

  // Update alarm
  chrome.alarms.clear(`poll_${sub.id}`);
  if (sub.status === 'active') {
    chrome.alarms.create(`poll_${sub.id}`, {
      periodInMinutes: payload.pollIntervalMinutes,
      delayInMinutes: 0.5,
    });
  }

  // Push to server
  const newVersion = await pushSubscriptionToServer(sub);
  if (newVersion !== null) {
    sub.version = newVersion;
    await StorageManager.saveSubscription(sub);
  }

  log(`Updated poll interval for "${sub.name}" to ${payload.pollIntervalMinutes}m`);
  return { ok: true };
}
```

- [ ] **Step 2: Add poll interval edit UI in ActiveSubscriptions.tsx**

In `src/chrome-extension/src/popup/ActiveSubscriptions.tsx`, add an editable poll interval field. Add state for the editing subscription:

```typescript
const [editingInterval, setEditingInterval] = useState<string | null>(null);
const [intervalValue, setIntervalValue] = useState<number>(5);
```

Add an edit button next to the poll interval display, and a small inline form:

```tsx
{editingInterval === sub.id ? (
  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mt: 0.5 }}>
    <TextField
      size="small"
      type="number"
      value={intervalValue}
      onChange={(e) => setIntervalValue(Number(e.target.value))}
      inputProps={{ min: 1, max: 1440, step: 1 }}
      sx={{ width: 80 }}
    />
    <Typography variant="caption">min</Typography>
    <IconButton size="small" onClick={() => {
      chrome.runtime.sendMessage({
        type: 'UPDATE_POLL_INTERVAL',
        payload: { subscriptionId: sub.id, pollIntervalMinutes: intervalValue },
      }, () => {
        setEditingInterval(null);
        onUpdate();
      });
    }}>
      <CheckIcon fontSize="small" />
    </IconButton>
    <IconButton size="small" onClick={() => setEditingInterval(null)}>
      <CloseIcon fontSize="small" />
    </IconButton>
  </Box>
) : (
  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
    <Typography variant="body2">Every {sub.pollIntervalMinutes}m</Typography>
    <IconButton size="small" onClick={() => {
      setEditingInterval(sub.id);
      setIntervalValue(sub.pollIntervalMinutes);
    }}>
      <EditIcon fontSize="small" />
    </IconButton>
  </Box>
)}
```

Add required imports:
```typescript
import { TextField, IconButton } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
```

- [ ] **Step 3: Build and verify**

Run:
```bash
cd src/chrome-extension && npx tsc --noEmit && npm run build
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/chrome-extension/src/background/service-worker.ts src/chrome-extension/src/popup/ActiveSubscriptions.tsx
git commit -m "feat: add editable poll interval with server sync"
```

---

### Task 9: Write backend tests

**Files:**
- Create: `src/LogJammer.Tests/DataSourceConcurrencyTests.cs`
- Create: `src/LogJammer.Tests/IngestPollGuardTests.cs`

- [ ] **Step 1: Write concurrency test**

Create `src/LogJammer.Tests/DataSourceConcurrencyTests.cs`:

```csharp
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogJammer.Tests;

[Collection("Database")]
public class DataSourceConcurrencyTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Update_WithMatchingVersion_Succeeds()
    {
        await using var db = fixture.CreateDbContext();
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"concurrency-test-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = "{}",
            Version = 1,
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Simulate update with correct version
        var loaded = await db.DataSources.FirstAsync(d => d.Id == source.Id);
        Assert.Equal(1, loaded.Version);
        loaded.Name = "updated-name";
        loaded.Version++;
        await db.SaveChangesAsync();

        var reloaded = await db.DataSources.AsNoTracking().FirstAsync(d => d.Id == source.Id);
        Assert.Equal("updated-name", reloaded.Name);
        Assert.Equal(2, reloaded.Version);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ThrowsConcurrencyException()
    {
        await using var db1 = fixture.CreateDbContext();
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"concurrency-stale-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = "{}",
            Version = 1,
        };
        db1.DataSources.Add(source);
        await db1.SaveChangesAsync();

        // Load in two separate contexts
        await using var db2 = fixture.CreateDbContext();
        var loaded1 = await db1.DataSources.FirstAsync(d => d.Id == source.Id);
        var loaded2 = await db2.DataSources.FirstAsync(d => d.Id == source.Id);

        // First update succeeds
        loaded1.Name = "first-update";
        loaded1.Version++;
        await db1.SaveChangesAsync();

        // Second update with stale version fails
        loaded2.Name = "second-update";
        loaded2.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }
}
```

- [ ] **Step 2: Run concurrency tests**

Run:
```bash
dotnet test src/LogJammer.slnx --filter "FullyQualifiedName~DataSourceConcurrencyTests"
```

Expected: 2 tests pass.

- [ ] **Step 3: Write poll guard test**

Create `src/LogJammer.Tests/IngestPollGuardTests.cs`:

```csharp
using System.Text.Json;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogJammer.Tests;

[Collection("Database")]
public class IngestPollGuardTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task KibanaProxy_WithRecentPoll_ShouldBeGuarded()
    {
        await using var db = fixture.CreateDbContext();
        var config = JsonSerializer.Serialize(new { pollIntervalMinutes = 5.0 });
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"guard-test-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = config,
            LastPolledAt = DateTimeOffset.UtcNow.AddMinutes(-1), // polled 1 min ago
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Threshold is 5 * 0.5 = 2.5 minutes. Last poll was 1 min ago → should be guarded.
        var timeSinceLastPoll = DateTimeOffset.UtcNow - source.LastPolledAt!.Value;
        var threshold = TimeSpan.FromMinutes(5 * 0.5);
        Assert.True(timeSinceLastPoll < threshold, "Poll should be within guard threshold");
    }

    [Fact]
    public async Task KibanaProxy_WithOldPoll_ShouldNotBeGuarded()
    {
        await using var db = fixture.CreateDbContext();
        var config = JsonSerializer.Serialize(new { pollIntervalMinutes = 5.0 });
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"guard-test-old-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = config,
            LastPolledAt = DateTimeOffset.UtcNow.AddMinutes(-10), // polled 10 min ago
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Threshold is 5 * 0.5 = 2.5 minutes. Last poll was 10 min ago → should NOT be guarded.
        var timeSinceLastPoll = DateTimeOffset.UtcNow - source.LastPolledAt!.Value;
        var threshold = TimeSpan.FromMinutes(5 * 0.5);
        Assert.False(timeSinceLastPoll < threshold, "Poll should not be within guard threshold");
    }

    [Fact]
    public async Task Elasticsearch_Type_ShouldNotBeGuarded()
    {
        await using var db = fixture.CreateDbContext();
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"guard-test-es-{Guid.NewGuid():N}",
            Type = DataSourceType.Elasticsearch,
            ConnectionConfig = "http://localhost:9200",
            LastPolledAt = DateTimeOffset.UtcNow.AddSeconds(-10), // very recent
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Elasticsearch type should never trigger the guard
        Assert.Equal(DataSourceType.Elasticsearch, source.Type);
    }
}
```

- [ ] **Step 4: Run all tests**

Run:
```bash
dotnet test src/LogJammer.slnx
```

Expected: All tests pass (existing + new).

- [ ] **Step 5: Commit**

```bash
git add src/LogJammer.Tests/DataSourceConcurrencyTests.cs src/LogJammer.Tests/IngestPollGuardTests.cs
git commit -m "test: add concurrency and poll guard tests"
```

---

### Task 10: Update spec docs

**Files:**
- Modify: `specs/definition-dto.md`
- Modify: `specs/definition-api.md`

- [ ] **Step 1: Update definition-dto.md**

Add `Version` to the DataSource entity table:

In the DataSource entity table (around line 47-57), add after `LastPolledAt`:
```
| Version | int | default 1; concurrency check token |
```

Update `UpdateDataSourceRequest` (around line 136-140) to add:
```
- `Version` (int) — required; must match server version for optimistic concurrency
```

Update `DataSourceResponse` (around line 147-150) to add:
```
- `Version` (int)
```

Update `IngestResponse` (around line 173-174) to:
```
#### IngestResponse
`LogJammer.Api.Dtos.IngestResponse` (record)
- `Accepted` (int) — number of entries passed to the ingestion pipeline
- `Skipped` (bool) — true if rejected by poll interval guard (default false)
- `Reason` (string?) — explanation when skipped (e.g., "Another client polled 30s ago")
```

Update the `ConnectionConfig` description in CreateDataSourceRequest (around line 131):
```
- `ConnectionConfig` (string) — JSON connection config; for Elasticsearch: ES URL string; for KibanaProxy: JSON object with `kibanaUrl`, `indexPattern`, `queryDsl`, `fullRequestBody?`, `selectedFields`, `messageTemplate`, `pollIntervalMinutes`, `subscriptionStatus`, `lastSubscribedAt`
```

- [ ] **Step 2: Update definition-api.md**

Update the PUT datasources endpoint (around line 55):
```
**PUT /api/datasources/{id}**
- Body: `UpdateDataSourceRequest` — null fields are ignored; `version` is required
- 200: `DataSourceResponse`
- 404
- 409: `{ "error": "conflict", "message": "DataSource was modified by another client", "currentVersion": <int> }`
```

Update the ingest endpoint (around line 131-137):
```
**POST /api/ingest/{dataSourceId}**
- Body: `IngestRequest` — array of up to 10 000 `IngestEntry` items
- 200: `IngestResponse` — `{ "accepted": <count>, "skipped": false }` or `{ "accepted": 0, "skipped": true, "reason": "..." }`
- 400: `{ "message": "Data source is disabled" }`
- 404: `{ "message": "Data source not found" }`

Notes: Works for both `KibanaProxy` and `Elasticsearch` data source types. For KibanaProxy, a poll interval guard rejects requests that arrive within 50% of the configured `pollIntervalMinutes` since `LastPolledAt`, returning `skipped: true` to prevent duplicate ingestion from multiple browser instances. The ingestion pipeline runs DrainParser, updates `PatternOccurrence` windows, and stores a new pattern if `IsNewCluster = true`.
```

- [ ] **Step 3: Commit**

```bash
git add specs/definition-dto.md specs/definition-api.md
git commit -m "docs: update specs with version, concurrency, and poll guard changes"
```

---

## Task Dependency Graph

```
Task 1 (Entity + Migration)
  └─→ Task 2 (DTOs)
        └─→ Task 3 (Controller concurrency)
        └─→ Task 4 (Ingest guard)
              └─→ Task 9 (Backend tests) [depends on 1-4]

Task 5 (Extension types) [independent of backend]
  └─→ Task 6 (Service worker sync)
        └─→ Task 7 (Popup UI sync)
        └─→ Task 8 (Poll interval edit + push)

Task 10 (Spec docs) [after all implementation]
```

**Parallelism:** Tasks 1-4 (backend) and Task 5 (extension types) can start in parallel. Tasks 6-8 depend on Task 5. Task 9 depends on Tasks 1-4. Task 10 runs last.
