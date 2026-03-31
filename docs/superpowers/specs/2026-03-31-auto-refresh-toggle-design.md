# Auto-Refresh Toggle Design

## Summary

Add a global auto-refresh toggle to the TopBar that lets users configure automatic data polling across Dashboard, Patterns, and DataSources pages. Options: Off / 1m / 5m. Setting persists to localStorage. Defaults to Off.

## Components

### AutoRefreshContext (new file: `src/frontend/src/AutoRefreshContext.tsx`)

A React context + provider, following the same pattern as the existing `ThemeContext.tsx`.

- **State:** `refreshInterval: number` — one of `0` (off), `60_000` (1m), `300_000` (5m)
- **Persistence:** `localStorage` key `logjammer-auto-refresh`. Defaults to `0` if no stored value or invalid value.
- **Exposes:** `{ refreshInterval, setRefreshInterval }`

### TopBar changes (`src/frontend/src/components/TopBar.tsx`)

Add a dropdown to the right side of the TopBar, before the existing theme toggle:

- A button/icon showing the current refresh setting (e.g., refresh icon + "Off" / "1m" / "5m")
- Clicking opens a Menu with three items: Off, 1m, 5m
- Active item visually indicated
- Consistent styling with the existing theme mode toggle

### Provider wiring (`src/frontend/src/main.tsx`)

Wrap the app with `<AutoRefreshProvider>` alongside the existing `<ThemeProvider>`.

### Hook changes

| Hook | File | Change |
|------|------|--------|
| `useDashboard` | `src/frontend/src/api/hooks/useDashboard.ts` | Remove hardcoded `refetchInterval: 30_000`, read from `useAutoRefresh()` context |
| `usePatterns` | `src/frontend/src/api/hooks/usePatterns.ts` | Add `refetchInterval` from context |
| `useDataSources` | `src/frontend/src/api/hooks/useDataSources.ts` | Add `refetchInterval` from context |

**Excluded:** `usePatternDetail` — single-item detail view, auto-refresh not useful here.

### Data flow

```
localStorage <-> AutoRefreshContext <-> TopBar (UI control)
                                    <-> useDashboard (refetchInterval)
                                    <-> usePatterns (refetchInterval)
                                    <-> useDataSources (refetchInterval)
```

## Behavior

- React Query's `refetchInterval` option natively supports `0` / `false` to mean no polling
- When interval is `0`, pass `false` for `refetchInterval` to disable polling
- When user switches interval, all active queries on the current page immediately adopt the new interval
- Setting persists across page reloads and sessions via localStorage

## Files changed

| File | Type |
|------|------|
| `src/frontend/src/AutoRefreshContext.tsx` | New |
| `src/frontend/src/components/TopBar.tsx` | Modified |
| `src/frontend/src/main.tsx` | Modified |
| `src/frontend/src/api/hooks/useDashboard.ts` | Modified |
| `src/frontend/src/api/hooks/usePatterns.ts` | Modified |
| `src/frontend/src/api/hooks/useDataSources.ts` | Modified |
