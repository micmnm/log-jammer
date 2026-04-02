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
  logJammerUrl: 'https://logjammer.mltru.com',
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
  proxyEndpoint?: string;
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
