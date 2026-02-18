// src/chrome-extension/src/background/service-worker.ts
import { StorageManager } from '../utils/storage';
import { summarizeQuery, extractIndexPattern } from '../shared/kibana-query-parser';
import type { CapturedQuery, Subscription, IngestEntry, IngestResponse } from '../shared/types';

// --- Verbose logging helper ---

async function isVerbose(): Promise<boolean> {
  const settings = await StorageManager.getSettings();
  return settings.verbose ?? false;
}

function log(...args: unknown[]): void {
  console.log('[LogJammer]', ...args);
}

async function vlog(...args: unknown[]): Promise<void> {
  if (await isVerbose()) console.log('[LogJammer][verbose]', ...args);
}

// --- Message handling (from content script) ---

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message.type === 'KIBANA_QUERY_CAPTURED') {
    handleCapturedQuery(message.payload).then(() => sendResponse({ ok: true }));
    return true; // async response
  }

  if (message.type === 'GET_STATE') {
    getState().then(state => sendResponse(state));
    return true;
  }

  if (message.type === 'SUBSCRIBE') {
    handleSubscribe(message.payload).then(result => sendResponse(result));
    return true;
  }

  if (message.type === 'UNSUBSCRIBE') {
    handleUnsubscribe(message.payload.subscriptionId).then(() => sendResponse({ ok: true }));
    return true;
  }

  if (message.type === 'UPDATE_SETTINGS') {
    StorageManager.saveSettings(message.payload).then(() => {
      log('Settings updated', message.payload.verbose ? '(verbose ON)' : '(verbose OFF)');
      sendResponse({ ok: true });
    });
    return true;
  }

  if (message.type === 'KIBANA_SESSION_ACTIVE') {
    resumePausedSubscriptions().then(() => sendResponse({ ok: true }));
    return true;
  }
});

async function handleCapturedQuery(payload: {
  url: string;
  method: string;
  queryDsl: Record<string, unknown>;
  fullRequestBody?: Record<string, unknown>;
  indexPattern: string;
  kibanaUrl: string;
  capturedAt: string;
}): Promise<void> {
  const query: CapturedQuery = {
    id: crypto.randomUUID(),
    kibanaUrl: payload.kibanaUrl,
    proxyEndpoint: payload.url,
    method: payload.method,
    indexPattern: payload.indexPattern ?? extractIndexPattern(payload.url, payload.queryDsl),
    queryDsl: payload.queryDsl,
    fullRequestBody: payload.fullRequestBody,
    summary: summarizeQuery(payload.queryDsl),
    capturedAt: payload.capturedAt,
  };
  log('Query captured:', query.summary, `[${query.indexPattern}]`);
  await vlog('Full request body:', JSON.stringify(query.fullRequestBody, null, 2));
  await StorageManager.addCapturedQuery(query);
}

async function getState() {
  const [queries, subscriptions, settings] = await Promise.all([
    StorageManager.getCapturedQueries(),
    StorageManager.getSubscriptions(),
    StorageManager.getSettings(),
  ]);
  return { queries, subscriptions, settings };
}

// --- Subscription management ---

async function handleSubscribe(payload: {
  queryId: string;
  name: string;
  pollIntervalMinutes?: number;
}): Promise<{ ok: boolean; error?: string; subscriptionId?: string }> {
  const queries = await StorageManager.getCapturedQueries();
  const query = queries.find(q => q.id === payload.queryId);
  if (!query) return { ok: false, error: 'Query not found' };

  const settings = await StorageManager.getSettings();
  const pollIntervalMinutes = payload.pollIntervalMinutes ?? settings.defaultPollIntervalMinutes ?? 5;

  log(`Creating subscription "${payload.name}" with poll interval ${pollIntervalMinutes}m`);

  // Create DataSource in Log Jammer
  try {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (settings.apiToken) headers['Authorization'] = `Bearer ${settings.apiToken}`;

    const dsResponse = await fetch(`${settings.logJammerUrl}/api/datasources`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        name: payload.name,
        adapterType: 'KibanaProxy',
        connectionConfig: JSON.stringify({
          kibanaUrl: query.kibanaUrl,
          indexPattern: query.indexPattern,
          queryDsl: query.queryDsl,
          capturedAt: query.capturedAt,
        }),
        pollIntervalSeconds: pollIntervalMinutes * 60,
        enabled: true,
      }),
    });

    if (!dsResponse.ok) {
      const error = await dsResponse.text();
      return { ok: false, error: `Failed to create DataSource: ${error}` };
    }

    const dataSource = await dsResponse.json() as { id: string };

    const subscription: Subscription = {
      id: crypto.randomUUID(),
      queryId: query.id,
      dataSourceId: dataSource.id,
      name: payload.name,
      pollIntervalMinutes,
      lastPollAt: null,
      lastError: null,
      status: 'active',
    };

    await StorageManager.saveSubscription(subscription);

    // Set up alarm
    chrome.alarms.create(`poll_${subscription.id}`, {
      periodInMinutes: pollIntervalMinutes,
      delayInMinutes: 0.5, // Chrome minimum is 0.5; fires soon, then on interval
    });

    log(`Subscription "${payload.name}" active, polling every ${pollIntervalMinutes}m`);
    return { ok: true, subscriptionId: subscription.id };
  } catch (err) {
    return { ok: false, error: `Network error: ${err instanceof Error ? err.message : String(err)}` };
  }
}

async function handleUnsubscribe(subscriptionId: string): Promise<void> {
  chrome.alarms.clear(`poll_${subscriptionId}`);
  await clearSeenDocIds(subscriptionId);
  log('Subscription removed:', subscriptionId);
  await StorageManager.removeSubscription(subscriptionId);
}

// --- Alarm-driven polling ---

chrome.alarms.onAlarm.addListener(async (alarm) => {
  if (!alarm.name.startsWith('poll_')) return;

  const subscriptionId = alarm.name.replace('poll_', '');
  const subscriptions = await StorageManager.getSubscriptions();
  const subscription = subscriptions.find(s => s.id === subscriptionId);
  if (!subscription || subscription.status !== 'active') return;

  const queries = await StorageManager.getCapturedQueries();
  const query = queries.find(q => q.id === subscription.queryId);
  if (!query) return;

  log(`Polling "${subscription.name}"...`);
  await executePoll(subscription, query);
});

async function executePoll(subscription: Subscription, query: CapturedQuery): Promise<void> {
  const settings = await StorageManager.getSettings();

  try {
    // Build the poll request body — force compress=false so we send/receive plain JSON
    const pollUrl = `${query.kibanaUrl}${query.proxyEndpoint}`.replace('compress=true', 'compress=false');
    let pollPayload: Record<string, unknown>;

    if (query.fullRequestBody) {
      // Replay the full bsearch request, adjusting time range inside the nested body
      pollPayload = adjustTimeRangeInFullRequest(query.fullRequestBody, subscription.lastPollAt);
    } else {
      // Legacy: no full body stored, send bare query DSL (may not work for bsearch)
      pollPayload = adjustTimeRange(query.queryDsl, subscription.lastPollAt);
    }

    const pollBody = JSON.stringify(pollPayload);
    log(`Poll "${subscription.name}" → ${query.method} ${pollUrl}`);
    await vlog(`Poll "${subscription.name}" request body:`, pollBody);

    const kibanaResponse = await fetch(pollUrl, {
      method: query.method,
      headers: { 'Content-Type': 'application/json', 'kbn-xsrf': 'true' },
      credentials: 'include',
      body: pollBody,
    });

    if (kibanaResponse.status === 401 || kibanaResponse.status === 403) {
      subscription.status = 'paused';
      const details = settings.errorDetails
        ? `\n--- error details ---\npoll URL: ${pollUrl}\nresponse: ${kibanaResponse.status}`
        : '';
      subscription.lastError = `Kibana session expired. Visit Kibana to re-authenticate.${details}`;
      await StorageManager.saveSubscription(subscription);
      log(`Poll "${subscription.name}" paused — Kibana session expired`);
      chrome.action.setBadgeText({ text: '!' });
      chrome.action.setBadgeBackgroundColor({ color: '#ff1744' });
      return;
    }

    if (!kibanaResponse.ok) {
      const errorBody = await kibanaResponse.text().catch(() => '(could not read body)');
      const details = settings.errorDetails
        ? `\n--- error details ---`
          + `\noriginal captured URL: ${query.proxyEndpoint}`
          + `\noriginal captured payload: ${JSON.stringify(query.queryDsl)}`
          + `\npoll request URL: ${pollUrl}`
          + `\npoll request payload: ${pollBody}`
          + `\nresponse: ${kibanaResponse.status}`
          + `\nresponse body: ${errorBody}`
        : '';
      subscription.lastError = `Kibana returned ${kibanaResponse.status}${details}`;
      await StorageManager.saveSubscription(subscription);
      log(`Poll "${subscription.name}" failed — Kibana returned ${kibanaResponse.status}`);
      return;
    }

    const data = await kibanaResponse.json() as Record<string, unknown>;
    const allHits = extractHits(data);

    if (allHits.length === 0) {
      subscription.lastPollAt = new Date().toISOString();
      subscription.lastError = null;
      await StorageManager.saveSubscription(subscription);
      log(`Poll "${subscription.name}" complete — 0 new hits`);
      return;
    }

    // Deduplicate: filter out documents already seen in previous polls
    const seenIds = await getSeenDocIds(subscription.id);
    const newHits = allHits.filter(hit => {
      const docId = hit._id as string | undefined;
      return !docId || !seenIds.has(docId);
    });

    log(`Poll "${subscription.name}" — ${allHits.length} hits from Kibana, ${newHits.length} new after dedup`);

    if (newHits.length === 0) {
      subscription.lastPollAt = new Date().toISOString();
      subscription.lastError = null;
      await StorageManager.saveSubscription(subscription);
      return;
    }

    // Track new document IDs
    const newDocIds = newHits.map(h => h._id as string).filter(Boolean);
    await addSeenDocIds(subscription.id, newDocIds);

    // Push to Log Jammer — Kibana may return _source or fields (when _source: false)
    const entries: IngestEntry[] = newHits.map(hit => {
      const source = hit._source as Record<string, unknown> | undefined;
      const fields = hit.fields as Record<string, unknown> | undefined;
      // fields values are arrays in ES response — flatten to first element
      const flatFields: Record<string, unknown> = {};
      if (fields) {
        for (const [key, val] of Object.entries(fields)) {
          flatFields[key] = Array.isArray(val) && val.length === 1 ? val[0] : val;
        }
      }
      const hitData = source ?? flatFields;
      // Include _id for backend-side dedup as well
      if (hit._id) hitData['_id'] = hit._id;
      return {
        timestamp: (hitData['@timestamp'] as string) ?? new Date().toISOString(),
        fields: hitData,
      };
    });

    const ingestHeaders: Record<string, string> = { 'Content-Type': 'application/json' };
    if (settings.apiToken) ingestHeaders['Authorization'] = `Bearer ${settings.apiToken}`;

    const ingestResponse = await fetch(
      `${settings.logJammerUrl}/api/ingest/${subscription.dataSourceId}`,
      {
        method: 'POST',
        headers: ingestHeaders,
        body: JSON.stringify({ entries }),
      }
    );

    if (!ingestResponse.ok) {
      const ingestError = await ingestResponse.text().catch(() => '(could not read body)');
      const details = settings.errorDetails
        ? `\n--- error details ---`
          + `\ningest URL: ${settings.logJammerUrl}/api/ingest/${subscription.dataSourceId}`
          + `\ningest payload: ${JSON.stringify({ entries })}`
          + `\ningest response: ${ingestError}`
        : '';
      subscription.lastError = `Log Jammer returned ${ingestResponse.status}${details}`;
      log(`Poll "${subscription.name}" ingest failed — ${ingestResponse.status}`);
    } else {
      const result = await ingestResponse.json() as IngestResponse;
      subscription.lastError = null;
      log(`Poll "${subscription.name}" complete — ${result.accepted} new, ${result.duplicates} duplicate entries`);
    }

    subscription.lastPollAt = new Date().toISOString();
    await StorageManager.saveSubscription(subscription);

  } catch (err) {
    subscription.lastError = err instanceof Error ? err.message : String(err);
    await StorageManager.saveSubscription(subscription);
    log(`Poll "${subscription.name}" error:`, subscription.lastError);
  }
}

function adjustTimeRange(
  queryDsl: Record<string, unknown>,
  lastPollAt: string | null
): Record<string, unknown> {
  if (!lastPollAt) return queryDsl;

  // Deep clone
  const adjusted = JSON.parse(JSON.stringify(queryDsl)) as Record<string, unknown>;

  // Try to find and update range filter on @timestamp
  const query = adjusted.query as Record<string, unknown> | undefined;
  if (!query) return adjusted;

  const bool = query.bool as Record<string, unknown> | undefined;
  if (!bool?.filter || !Array.isArray(bool.filter)) return adjusted;

  for (const clause of bool.filter as Record<string, unknown>[]) {
    if ('range' in clause) {
      const range = clause.range as Record<string, Record<string, unknown>>;
      if ('@timestamp' in range) {
        range['@timestamp'].gte = lastPollAt;
        range['@timestamp'].lte = 'now';
        return adjusted;
      }
    }
  }

  // No existing range found — add one
  (bool.filter as Record<string, unknown>[]).push({
    range: { '@timestamp': { gte: lastPollAt, lte: 'now' } }
  });
  return adjusted;
}

// --- Document ID deduplication ---
// Stores recently seen document IDs per subscription (bounded to prevent unbounded growth)

const MAX_SEEN_IDS = 5000;

async function getSeenDocIds(subscriptionId: string): Promise<Set<string>> {
  const key = `lj_seen_${subscriptionId}`;
  const result = await chrome.storage.local.get([key]);
  const ids = result[key] as string[] | undefined;
  return new Set(ids ?? []);
}

async function addSeenDocIds(subscriptionId: string, newIds: string[]): Promise<void> {
  if (newIds.length === 0) return;
  const key = `lj_seen_${subscriptionId}`;
  const existing = await getSeenDocIds(subscriptionId);
  for (const id of newIds) existing.add(id);
  // Keep only the most recent MAX_SEEN_IDS
  const all = Array.from(existing);
  const trimmed = all.length > MAX_SEEN_IDS ? all.slice(all.length - MAX_SEEN_IDS) : all;
  await chrome.storage.local.set({ [key]: trimmed });
}

async function clearSeenDocIds(subscriptionId: string): Promise<void> {
  await chrome.storage.local.remove(`lj_seen_${subscriptionId}`);
}

function adjustTimeRangeInFullRequest(
  fullRequestBody: Record<string, unknown>,
  lastPollAt: string | null
): Record<string, unknown> {
  // Deep clone
  const adjusted = JSON.parse(JSON.stringify(fullRequestBody)) as Record<string, unknown>;

  // Navigate into batch[0].request.params.body to find the query DSL
  const batch = adjusted.batch as Record<string, unknown>[] | undefined;
  if (batch && batch.length > 0) {
    const request = batch[0].request as Record<string, unknown> | undefined;
    const params = request?.params as Record<string, unknown> | undefined;
    if (params?.body) {
      params.body = adjustTimeRange(params.body as Record<string, unknown>, lastPollAt);
    }
    return adjusted;
  }

  // Not a batch format — fall back to adjusting directly
  return adjustTimeRange(adjusted, lastPollAt);
}

function extractHits(data: Record<string, unknown>): Array<Record<string, unknown>> {
  // Standard ES response: { hits: { hits: [...] } }
  if (data.hits && typeof data.hits === 'object') {
    const hits = data.hits as Record<string, unknown>;
    if (Array.isArray(hits.hits)) return hits.hits as Array<Record<string, unknown>>;
  }

  // Kibana bsearch wraps in result.rawResponse
  if (data.result && typeof data.result === 'object') {
    return extractHits(data.result as Record<string, unknown>);
  }

  // Kibana bsearch wraps in rawResponse
  if (data.rawResponse && typeof data.rawResponse === 'object') {
    return extractHits(data.rawResponse as Record<string, unknown>);
  }

  // Kibana bsearch array response
  if (Array.isArray(data)) {
    for (const item of data) {
      if (typeof item === 'object' && item !== null) {
        const hits = extractHits(item as Record<string, unknown>);
        if (hits.length > 0) return hits;
      }
    }
  }

  return [];
}

// --- Session resume: re-activate paused subscriptions when Kibana page loads ---

async function resumePausedSubscriptions(): Promise<void> {
  const subscriptions = await StorageManager.getSubscriptions();
  let resumed = false;
  for (const sub of subscriptions) {
    if (sub.status === 'paused') {
      sub.status = 'active';
      sub.lastError = null;
      await StorageManager.saveSubscription(sub);
      chrome.alarms.create(`poll_${sub.id}`, {
        periodInMinutes: sub.pollIntervalMinutes,
        delayInMinutes: 0.5,
      });
      log(`Resumed subscription "${sub.name}"`);
      resumed = true;
    }
  }
  if (resumed) {
    chrome.action.setBadgeText({ text: '' });
  }
}

// --- Startup: restore alarms for active subscriptions ---

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
}

restoreAlarms();
