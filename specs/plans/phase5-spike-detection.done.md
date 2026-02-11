# Phase 5: Spike Detection & Alert System

**Status:** draft
**Depends on:** Phase 3
**Blocks:** Phase 8
**Parallel with:** Phase 4

---

## Goal

Detect spikes per error group and manage alert lifecycle with capped escalation.

---

## Tasks

### 5.1 Spike detection engine
- Per-group evaluation on each processing cycle
- Threshold types (MVP):
  - **Absolute**: count exceeds N in window
  - **Percentage increase**: rate increases by X% vs. rolling average baseline
- Baseline calculation: rolling average over configurable lookback (default 24h)
- Cold start: absolute thresholds only until 24h of data exists

### 5.2 Alert manager
- Create alert when spike detected
- Alert lifecycle state machine:
  - `firing` → `firing (suppressed)` → `resolved`
  - `firing` → `acknowledged` → `resolved`
- Capped escalation: max 1 reminder per 10 min, max 5 notifications total
- 5th notification includes suppression message
- Auto-resolve: rate below threshold for 2 consecutive evaluation windows
- User acknowledge: stops notifications immediately
- Deduplication: one active alert per error group
- Re-fire: new alert after resolution if spike recurs (counter resets)

### 5.3 Cross-group correlation (basic)
- If 3+ groups from same data source spike within same 5-min window → correlated spike alert
- Show common attributes across correlated groups (e.g., same service, same host)

### 5.4 Spike detection configuration API
- CRUD for detection rules (per error group or global defaults)
- Configure: threshold type, value, time window, lookback period

### 5.5 Alert API
- List active alerts (with filtering by status, severity, data source)
- Acknowledge alert endpoint
- Alert history (past alerts with resolution info)

### 5.6 Tests
- Spike detection unit tests (various threshold scenarios)
- Alert lifecycle state machine tests (all transitions)
- Capped escalation logic tests
- Cross-group correlation tests
- Cold start behavior tests

---

## Deliverable

System detects error spikes, manages alert lifecycle with capped escalation, supports correlated spike detection.

---

## Acceptance Criteria

- [ ] Absolute threshold fires alert when count exceeds N in window
- [ ] Percentage threshold fires alert when rate exceeds X% over baseline
- [ ] Cold start uses absolute thresholds only (no percentage until 24h baseline)
- [ ] Alert lifecycle transitions work correctly (firing → suppressed → resolved)
- [ ] Capped escalation: max 5 notifications, max 1 per 10 min
- [ ] Auto-resolve after 2 consecutive windows below threshold
- [ ] User acknowledge stops notifications immediately
- [ ] One active alert per error group (deduplication)
- [ ] Correlated spike detected when 3+ groups spike in same 5-min window
- [ ] Alert API lists, filters, and returns history correctly
- [ ] Detection rules configurable per group and globally
