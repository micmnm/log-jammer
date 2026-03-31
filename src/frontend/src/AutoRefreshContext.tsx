import { createContext, useContext, useState, useMemo, type ReactNode } from 'react';

type RefreshInterval = 0 | 60_000 | 300_000;

interface AutoRefreshContextValue {
  refreshInterval: RefreshInterval;
  setRefreshInterval: (interval: RefreshInterval) => void;
}

const AutoRefreshContext = createContext<AutoRefreshContextValue>({
  refreshInterval: 0,
  setRefreshInterval: () => {},
});

const STORAGE_KEY = 'logjammer-auto-refresh';

const VALID_INTERVALS: RefreshInterval[] = [0, 60_000, 300_000];

export function AutoRefreshProvider({ children }: { children: ReactNode }) {
  const [refreshInterval, setRefreshIntervalState] = useState<RefreshInterval>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    const parsed = Number(stored);
    if (VALID_INTERVALS.includes(parsed as RefreshInterval)) return parsed as RefreshInterval;
    return 0;
  });

  function setRefreshInterval(interval: RefreshInterval) {
    setRefreshIntervalState(interval);
    localStorage.setItem(STORAGE_KEY, String(interval));
  }

  const value = useMemo(() => ({ refreshInterval, setRefreshInterval }), [refreshInterval]);

  return (
    <AutoRefreshContext.Provider value={value}>
      {children}
    </AutoRefreshContext.Provider>
  );
}

export function useAutoRefresh() {
  return useContext(AutoRefreshContext);
}
