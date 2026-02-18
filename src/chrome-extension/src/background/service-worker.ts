// src/chrome-extension/src/background/service-worker.ts
import { StorageManager } from '../utils/storage';
import { summarizeQuery, extractIndexPattern } from '../shared/kibana-query-parser';
import type { CapturedQuery, Subscription, IngestEntry, IngestResponse } from '../shared/types';

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
    StorageManager.saveSettings(message.payload).then(() => sendResponse({ ok: true }));
    return true;
  }
});

async function handleCapturedQuery(payload: {
  url: string;
  method: string;
  queryDsl: Record<string, unknown>;
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
    summary: summarizeQuery(payload.queryDsl),
    capturedAt: payload.capturedAt,
  };
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
  pollIntervalMinutes: number;
}): Promise<{ ok: boolean; error?: string; subscriptionId?: string }> {
  const queries = await StorageManager.getCapturedQueries();
  const query = queries.find(q => q.id === payload.queryId);
  if (!query) return { ok: false, error: 'Query not found' };

  const settings = await StorageManager.getSettings();

  // Create DataSource in Log Jammer
  try {
    const dsResponse = await fetch(`${settings.logJammerUrl}/api/datasources`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: payload.name,
        adapterType: 'KibanaProxy',
        connectionConfig: JSON.stringify({
          kibanaUrl: query.kibanaUrl,
          indexPattern: query.indexPattern,
          queryDsl: query.queryDsl,
          capturedAt: query.capturedAt,
        }),
        pollIntervalSeconds: payload.pollIntervalMinutes * 60,
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
      pollIntervalMinutes: payload.pollIntervalMinutes,
      lastPollAt: null,
      lastError: null,
      status: 'active',
    };

    await StorageManager.saveSubscription(subscription);

    // Set up alarm
    chrome.alarms.create(`poll_${subscription.id}`, {
      periodInMinutes: payload.pollIntervalMinutes,
      delayInMinutes: 0, // Fire immediately, then on interval
    });

    return { ok: true, subscriptionId: subscription.id };
  } catch (err) {
    return { ok: false, error: `Network error: ${err instanceof Error ? err.message : String(err)}` };
  }
}

async function handleUnsubscribe(subscriptionId: string): Promise<void> {
  chrome.alarms.clear(`poll_${subscriptionId}`);
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

  await executePoll(subscription, query);
});

async function executePoll(subscription: Subscription, query: CapturedQuery): Promise<void> {
  const settings = await StorageManager.getSettings();

  try {
    // Adjust time range for incremental polling
    const adjustedQuery = adjustTimeRange(query.queryDsl, subscription.lastPollAt);

    // Execute query through Kibana's proxy
    const kibanaResponse = await fetch(`${query.kibanaUrl}${query.proxyEndpoint}`, {
      method: query.method,
      headers: { 'Content-Type': 'application/json', 'kbn-xsrf': 'true' },
      credentials: 'include',
      body: JSON.stringify(adjustedQuery),
    });

    if (kibanaResponse.status === 401 || kibanaResponse.status === 403) {
      subscription.status = 'paused';
      subscription.lastError = 'Kibana session expired. Visit Kibana to re-authenticate.';
      await StorageManager.saveSubscription(subscription);
      chrome.action.setBadgeText({ text: '!' });
      chrome.action.setBadgeBackgroundColor({ color: '#ff1744' });
      return;
    }

    if (!kibanaResponse.ok) {
      subscription.lastError = `Kibana returned ${kibanaResponse.status}`;
      await StorageManager.saveSubscription(subscription);
      return;
    }

    const data = await kibanaResponse.json() as Record<string, unknown>;
    const hits = extractHits(data);

    if (hits.length === 0) {
      subscription.lastPollAt = new Date().toISOString();
      subscription.lastError = null;
      await StorageManager.saveSubscription(subscription);
      return;
    }

    // Push to Log Jammer
    const entries: IngestEntry[] = hits.map(hit => ({
      timestamp: (hit._source as Record<string, unknown>)?.['@timestamp'] as string
        ?? new Date().toISOString(),
      fields: hit._source as Record<string, unknown> ?? {},
    }));

    const ingestResponse = await fetch(
      `${settings.logJammerUrl}/api/ingest/${subscription.dataSourceId}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ entries }),
      }
    );

    if (!ingestResponse.ok) {
      subscription.lastError = `Log Jammer returned ${ingestResponse.status}`;
    } else {
      const result = await ingestResponse.json() as IngestResponse;
      subscription.lastError = null;
      console.log(`[LogJammer] Pushed ${result.accepted} new, ${result.duplicates} duplicate entries`);
    }

    subscription.lastPollAt = new Date().toISOString();
    await StorageManager.saveSubscription(subscription);

  } catch (err) {
    subscription.lastError = err instanceof Error ? err.message : String(err);
    await StorageManager.saveSubscription(subscription);
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

function extractHits(data: Record<string, unknown>): Array<Record<string, unknown>> {
  // Standard ES response
  if (data.hits && typeof data.hits === 'object') {
    const hits = data.hits as Record<string, unknown>;
    if (Array.isArray(hits.hits)) return hits.hits as Array<Record<string, unknown>>;
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

// --- Startup: restore alarms for active subscriptions ---

async function restoreAlarms(): Promise<void> {
  const subscriptions = await StorageManager.getSubscriptions();
  for (const sub of subscriptions) {
    if (sub.status === 'active') {
      chrome.alarms.create(`poll_${sub.id}`, {
        periodInMinutes: sub.pollIntervalMinutes,
        delayInMinutes: 1,
      });
    }
  }
}

restoreAlarms();
