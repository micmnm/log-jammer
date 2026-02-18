import type { CapturedQuery, Subscription, ExtensionSettings } from '../shared/types';
import { DEFAULT_SETTINGS } from '../shared/types';

const KEYS = {
  settings: 'lj_settings',
  queries: 'lj_captured_queries',
  subscriptions: 'lj_subscriptions',
} as const;

export const StorageManager = {
  async getSettings(): Promise<ExtensionSettings> {
    const result = await chrome.storage.local.get([KEYS.settings]);
    return (result[KEYS.settings] as ExtensionSettings) ?? { ...DEFAULT_SETTINGS };
  },

  async saveSettings(settings: ExtensionSettings): Promise<void> {
    await chrome.storage.local.set({ [KEYS.settings]: settings });
  },

  async getCapturedQueries(): Promise<CapturedQuery[]> {
    const result = await chrome.storage.local.get([KEYS.queries]);
    return (result[KEYS.queries] as CapturedQuery[]) ?? [];
  },

  async addCapturedQuery(query: CapturedQuery): Promise<void> {
    const settings = await this.getSettings();
    const queries = await this.getCapturedQueries();

    const existing = queries.findIndex(
      q => JSON.stringify(q.queryDsl) === JSON.stringify(query.queryDsl)
        && q.indexPattern === query.indexPattern
    );
    if (existing >= 0) {
      queries[existing] = { ...query, id: queries[existing].id };
    } else {
      queries.unshift(query);
    }

    const trimmed = queries.slice(0, settings.maxCapturedQueries);
    await chrome.storage.local.set({ [KEYS.queries]: trimmed });
  },

  async getSubscriptions(): Promise<Subscription[]> {
    const result = await chrome.storage.local.get([KEYS.subscriptions]);
    return (result[KEYS.subscriptions] as Subscription[]) ?? [];
  },

  async saveSubscription(subscription: Subscription): Promise<void> {
    const subs = await this.getSubscriptions();
    const idx = subs.findIndex(s => s.id === subscription.id);
    if (idx >= 0) {
      subs[idx] = subscription;
    } else {
      subs.push(subscription);
    }
    await chrome.storage.local.set({ [KEYS.subscriptions]: subs });
  },

  async removeSubscription(subscriptionId: string): Promise<void> {
    const subs = await this.getSubscriptions();
    await chrome.storage.local.set({
      [KEYS.subscriptions]: subs.filter(s => s.id !== subscriptionId),
    });
  },
};
