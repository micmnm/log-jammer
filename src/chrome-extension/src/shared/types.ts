export interface CapturedQuery {
  id: string;
  kibanaUrl: string;
  proxyEndpoint: string;
  method: string;
  indexPattern: string;
  queryDsl: Record<string, unknown>;
  summary: string;
  capturedAt: string;
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
}

export interface ExtensionSettings {
  logJammerUrl: string;
  apiToken: string;
  maxCapturedQueries: number;
}

export const DEFAULT_SETTINGS: ExtensionSettings = {
  logJammerUrl: 'http://localhost:5050',
  apiToken: '',
  maxCapturedQueries: 50,
};

export interface IngestEntry {
  timestamp: string;
  fields: Record<string, unknown>;
}

export interface IngestResponse {
  accepted: number;
  duplicates: number;
  failed: number;
}
