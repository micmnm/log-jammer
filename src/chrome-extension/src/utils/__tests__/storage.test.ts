import { StorageManager } from '../storage';
import type { CapturedQuery, Subscription, ExtensionSettings } from '../../shared/types';
import { DEFAULT_SETTINGS } from '../../shared/types';

const mockStorage: Record<string, unknown> = {};
const chromeMock = {
  storage: {
    local: {
      get: vi.fn((keys: string[]) =>
        Promise.resolve(
          Object.fromEntries(keys.map(k => [k, mockStorage[k]]))
        )
      ),
      set: vi.fn((items: Record<string, unknown>) => {
        Object.assign(mockStorage, items);
        return Promise.resolve();
      }),
    },
  },
};
vi.stubGlobal('chrome', chromeMock);

describe('StorageManager', () => {
  beforeEach(() => {
    Object.keys(mockStorage).forEach(k => delete mockStorage[k]);
    vi.clearAllMocks();
  });

  it('returns default settings when none stored', async () => {
    const settings = await StorageManager.getSettings();
    expect(settings).toEqual(DEFAULT_SETTINGS);
  });

  it('saves and retrieves settings', async () => {
    const custom: ExtensionSettings = { logJammerUrl: 'http://example.com', apiKey: 'test-key', maxCapturedQueries: 100, defaultPollIntervalMinutes: 5, verbose: false, errorDetails: false };
    await StorageManager.saveSettings(custom);
    const settings = await StorageManager.getSettings();
    expect(settings.logJammerUrl).toBe('http://example.com');
  });

  it('adds and retrieves captured queries', async () => {
    const query: CapturedQuery = {
      id: 'q1',
      kibanaUrl: 'https://kibana.corp.com',
      proxyEndpoint: '/internal/bsearch',
      method: 'POST',
      indexPattern: 'logs-*',
      queryDsl: { query: { match_all: {} } },
      summary: '(all documents)',
      capturedAt: new Date().toISOString(),
    };
    await StorageManager.addCapturedQuery(query);
    const queries = await StorageManager.getCapturedQueries();
    expect(queries).toHaveLength(1);
    expect(queries[0].id).toBe('q1');
  });

  it('limits stored queries to maxCapturedQueries', async () => {
    await StorageManager.saveSettings({ ...DEFAULT_SETTINGS, maxCapturedQueries: 2 });
    for (let i = 0; i < 3; i++) {
      await StorageManager.addCapturedQuery({
        id: `q${i}`,
        kibanaUrl: 'https://kibana.corp.com',
        proxyEndpoint: '/internal/bsearch',
        method: 'POST',
        indexPattern: 'logs-*',
        queryDsl: { uniqueField: i },
        summary: `query ${i}`,
        capturedAt: new Date().toISOString(),
      });
    }
    const queries = await StorageManager.getCapturedQueries();
    expect(queries.length).toBeLessThanOrEqual(2);
  });

  it('saves and retrieves subscriptions', async () => {
    const sub: Subscription = {
      id: 's1',
      queryId: 'q1',
      dataSourceId: 'ds-guid',
      name: 'Prod Errors',
      pollIntervalMinutes: 5,
      lastPollAt: null,
      lastError: null,
      status: 'active',
      selectedFields: ['service.name', 'message'],
      messageTemplate: '{service.name} | {message}',
      version: 1,
    };
    await StorageManager.saveSubscription(sub);
    const subs = await StorageManager.getSubscriptions();
    expect(subs).toHaveLength(1);
    expect(subs[0].name).toBe('Prod Errors');
  });
});
