# Phase 3: Error Processing Pipeline

**Status:** draft
**Depends on:** Phase 2
**Blocks:** Phase 4, Phase 5

---

## Goal

Implement the core processing loop: poll -> parse -> fingerprint -> store/update occurrences.

---

## Tasks

### 3.1 Schema mapping engine
- Map source fields to Log Jammer internal model (message, timestamp, severity, stack_trace, custom fields)
- Configurable per data source via the `fingerprint_configs` table

### 3.2 Fingerprint calculator
- Normalize error text: strip line numbers, memory addresses, timestamps, request IDs, UUIDs
- Compute SHA-256 hash from configured fields (normalized)
- Look up against known error library

### 3.3 Processing pipeline (background service)
- `IHostedService` / `BackgroundService` per active data source
- Scheduler: poll at configured interval (default 30s)
- Pipeline steps:
  1. Poll adapter
  2. Parse via schema mapping
  3. Compute fingerprint
  4. Known fingerprint? → Update occurrence count
  5. New fingerprint? → Queue for classification (Phase 4)
  6. Update time-window aggregates
- Adaptive sampling: when poll returns > budget (default 500), prioritize diversity + random sample, record sample ratio

### 3.4 Occurrence tracking
- Time-bucketed counts in `error_occurrences` (5m, 15m, 1h windows)
- Rolling window updates
- Extrapolate counts when sampling is active

### 3.5 Known error library auto-enrollment
- New fingerprint → create `known_errors` record with status `active`
- Store representative message + stack trace

### 3.6 Data retention job
- Background job (daily): prune occurrence data older than 30 days
- Keep error library entries indefinitely (just prune occurrence windows)

### 3.7 Backpressure monitoring
- Track when sampling is active per data source
- Expose via API for dashboard warning

### 3.8 Tests
- Fingerprint normalization tests (strip line numbers, UUIDs, etc.)
- Pipeline integration tests
- Sampling logic tests

---

## Deliverable

Background services polling configured data sources, fingerprinting errors, building the known error library, tracking occurrences per time window.

---

## Acceptance Criteria

- [ ] Background service starts polling on data source creation
- [ ] Schema mapping correctly extracts fields from raw log entries
- [ ] Fingerprint normalization strips variable parts (line numbers, UUIDs, timestamps, memory addresses)
- [ ] Same logical error produces same fingerprint across different requests
- [ ] Known errors have occurrence counts updated per time window (5m, 15m, 1h)
- [ ] New fingerprints auto-enroll in the known error library
- [ ] Adaptive sampling kicks in when poll exceeds budget, with diversity preserved
- [ ] Backpressure API reports sampling status per data source
- [ ] Retention job prunes old occurrence data but keeps error library entries
- [ ] Pipeline handles adapter failures gracefully (one source failing doesn't block others)
