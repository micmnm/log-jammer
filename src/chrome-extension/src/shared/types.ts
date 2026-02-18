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
  defaultPollIntervalMinutes: number;
  verbose: boolean;
  errorDetails: boolean;
}

export const DEFAULT_SETTINGS: ExtensionSettings = {
  logJammerUrl: 'http://localhost:5000',
  apiToken: '',
  maxCapturedQueries: 50,
  defaultPollIntervalMinutes: 5,
  verbose: false,
  errorDetails: false,
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
