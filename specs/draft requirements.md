# Log Jammer - Requirements v3

## Overview

Log Jammer is a proactive monitoring application that detects significant events in applications by analyzing structured logs. It uses a local embedding-based ML model to automatically classify, group, and fingerprint errors — then detects spikes, new error types, and recurring issues to enable faster incident response.

## Core Concepts

### Error Fingerprint

A fingerprint uniquely identifies an error *type*. It is a composite key derived from user-configured fields per data source:

- **Components**: application name + error message text (normalized) + stack trace (normalized)
- **Normalization**: Before hashing, variable parts are stripped — line numbers, memory addresses, timestamps, request IDs, UUIDs — so that the "same error in a different request" produces the same fingerprint
- **User-configurable**: Users select which fields from their log schema participate in the fingerprint (e.g., `service`, `error.type`, `error.message`, `stack_trace`)
- **Storage**: Stored as a stable hash (e.g., SHA-256 of the normalized composite key)

### Error Group

An error group is a collection of log entries sharing the same fingerprint. Each group tracks:

- Fingerprint hash
- First seen / last seen timestamps
- Occurrence count (total + per time window)
- Current status: `active`, `resolved`, `ignored`, `expected`
- Assigned tags/categories
- User overrides (if any)

### Tags & Categories

A flexible labeling system applied to error groups:

- **Auto-assigned** by the ML classifier (e.g., `timeout`, `auth-failure`, `database`, `oom`, `network`)
- **User-assigned** via the UI — users can add, remove, or override tags
- **Severity tags**: `critical`, `warning`, `info` (auto-assigned, user-overridable)
- **Custom tags**: Free-form, user-created (e.g., `team:payments`, `sprint-42`, `known-issue`)
- Tags are **not mutually exclusive** — an error can be tagged both `timeout` and `database`

---

## 1. Data Source Integration

### Adapter Pattern

Each data source is a pluggable adapter implementing a common interface:

```
IDataSourceAdapter
  - Connect / TestConnection
  - PollErrors(since: timestamp, limit: int) → ErrorBatch
  - GetSampleRecords(count: int) → RawLogEntry[]
  - GetSchema() → FieldDefinition[]
```

### Supported Sources (MVP)

| Source | Notes |
|--------|-------|
| Elasticsearch / OpenSearch | Query via REST API, configurable index pattern |
| Loki | LogQL queries via HTTP API |
| PostgreSQL | Direct SQL queries against log tables |

### Data Source Configuration

Per data source, the user configures:

- Connection details (URL, credentials, index/table)
- **Poll interval** (default: 30 seconds, configurable per source)
- **Fingerprint fields**: Which log fields compose the error fingerprint
- **Sampling strategy** (see below)
- **Log schema mapping**: Map source fields to Log Jammer's internal model (message, timestamp, severity, stack trace, etc.)

### Ingestion Model: Pull with Sampling

Log Jammer **pulls** from each data source on a configurable schedule.

**When volume exceeds processing capacity:**

- **Adaptive sampling**: If a poll returns more errors than the processing budget (configurable, default 500/poll), sample using:
  1. **Prioritize diversity** — ensure at least 1 representative from each unique fingerprint
  2. **Random sample** the remainder to stay within budget
  3. **Extrapolate counts** — record the sample ratio so spike detection can estimate true volume
- **Backpressure signal**: Surface a warning in the dashboard when sampling is active ("Processing 30% of errors — consider increasing poll budget or adding filters")

---

## 2. Error Detection & Classification

### Classification Engine: Local Embedding Model

The classification system is built around a **local embedding model** running on-CPU (no GPU required).

#### Model Selection Criteria

- Must run efficiently on CPU
- Must produce meaningful embeddings for log/error text
- Candidate approaches:
  - **Sentence-transformers** (e.g., `all-MiniLM-L6-v2`) via ONNX Runtime for .NET
  - **FastText** embeddings trained/fine-tuned on error logs
  - **TF-IDF + dimensionality reduction** as a lightweight fallback
- Model runs in-process within the .NET application via ONNX Runtime or ML.NET

#### How Classification Works

The pipeline processes each error in 4 stages:

```
[Raw Log Entry]
      │
      ▼
┌─────────────┐
│ 1. PARSE    │  Extract fields using schema mapping
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 2. FINGER-  │  Compute fingerprint from configured fields
│    PRINT    │  Check against known error library
└──────┬──────┘
       │
       ├── Known fingerprint → Update occurrence count, skip to step 4
       │
       ├── Unknown fingerprint ↓
       │
       ▼
┌─────────────┐
│ 3. CLASSIFY │  Generate embedding for the error text
│             │  Find nearest neighbors in the embedding space
│             │  Auto-assign tags based on similarity clusters
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 4. DETECT   │  Evaluate spike detection rules
│             │  Flag new/unknown errors
│             │  Update time-window aggregates
└──────┬──────┘
       │
       ▼
   [Alerts / Dashboard]
```

#### Stage 3 Detail: Classification via Embeddings

1. **Embed** the error message + stack trace (normalized) into a vector
2. **Nearest-neighbor search** against all existing error group embeddings (stored in-memory or via pgvector)
3. **Similarity threshold** (configurable, default cosine similarity > 0.85):
   - **Above threshold**: Merge into existing group, inherit its tags
   - **Below threshold**: Create new error group, auto-assign tags via cluster analysis
4. **Auto-tagging**:
   - The classifier maintains tag centroids (average embedding per tag)
   - New errors are tagged with the closest matching tag centroids above a confidence threshold
   - Low-confidence classifications are surfaced for user review ("Unclassified" queue)

#### User-Driven Learning Loop

The system learns from user actions:

1. **Sample Review**: Users can browse sample log entries from any error group
2. **Manual Classification**: Users assign/correct tags on error groups
3. **System Learns**: When a user corrects a classification:
   - The error group's tags are updated immediately
   - The tag centroid is recalculated to incorporate the correction
   - Future similar errors benefit from the correction
4. **Proposed Classifications**: System surfaces "suggested tags" for unclassified errors, users confirm or reject
5. **Merge/Split**: Users can merge two error groups (if the system over-split) or split one (if under-split) — fingerprint rules adjust accordingly

#### Override System

Users can override automatic behavior at multiple levels:

| Override Level | What It Does | Example |
|---------------|-------------|---------|
| **Tag Override** | Change tags on an error group | Remove auto-tag `database`, add `network` |
| **Severity Override** | Force severity regardless of auto-detection | Mark a noisy error as `info` |
| **Status Override** | Set group status to `ignored` or `expected` | Suppress alerts for a known flaky test |
| **Fingerprint Override** | Modify which fields compose the fingerprint | Add `user_id` to split a group by user |
| **Classification Override** | Pin a group to specific tags permanently | Prevent re-classification on model updates |

Overrides are **sticky** — they persist across model updates and reclassification runs.

---

## 3. Known Error Library

### Auto-Enrollment

Every new unique fingerprint is automatically enrolled in the library with:

- Fingerprint hash
- Representative error message (first occurrence)
- Embedding vector
- Auto-assigned tags and severity
- Status: `active` (default)
- First seen timestamp
- Occurrence timeline (count per time window)

### Library Record Schema

```
KnownError
  - id: UUID
  - fingerprint_hash: string
  - representative_message: text
  - representative_stack_trace: text
  - embedding_vector: float[]
  - tags: string[]                    -- e.g., ["timeout", "database", "critical"]
  - severity: enum (critical, warning, info)
  - status: enum (active, resolved, ignored, expected)
  - first_seen: timestamp
  - last_seen: timestamp
  - total_occurrences: long
  - occurrence_windows: jsonb         -- { "5m": 12, "15m": 34, "1h": 156 }
  - user_overrides: jsonb             -- tracks manual overrides
  - data_source_id: FK
  - created_at: timestamp
  - updated_at: timestamp
```

### Retention

- **Error groups with occurrences in the last 30 days**: Fully retained with all occurrence data
- **Error groups with no occurrences in 30+ days**: Occurrence data pruned, fingerprint + embedding + tags retained indefinitely (the library remembers errors even if they stop occurring)

---

## 4. Spike Detection

### Per-Group Detection

Spikes are detected **per error group**, not globally. Each group's occurrence rate is tracked independently.

### Threshold Types

| Type | Description | Example Config |
|------|-------------|---------------|
| **Absolute** | Count exceeds N in window | > 100 errors in 5 min |
| **Percentage increase** | Rate increases by X% vs. baseline | 200% increase over 1-hour rolling avg |
| **Standard deviation** | Rate exceeds N sigma from baseline | > 3 sigma from 24-hour rolling avg |

### Baseline Calculation

- **Rolling average** computed over a configurable lookback (default: 24 hours)
- **Time-of-day awareness**: Compare to the same hour in previous days to account for daily traffic patterns
- **Cold start**: For new error groups (< 24 hours of data), use absolute thresholds only until sufficient baseline exists

### Cross-Group Correlation (MVP: Basic)

When multiple error groups spike simultaneously:

- **Correlated spike detection**: If 3+ groups from the same data source spike within the same 5-minute window, surface a "correlated spike" alert
- **Common attributes**: Show shared attributes across correlated groups (e.g., same service, same host)
- Future: Automated root-cause suggestion based on correlation patterns

### Alert Lifecycle & Escalation

Alerts follow a **capped escalation** model — persistent but not annoying:

1. **Spike starts**: Alert fires with status `firing`, first notification appears
2. **Reminder cadence**: If unacknowledged, the alert re-notifies at **max 1 reminder per 10 minutes**
3. **Max 5 notifications total**: After 5 notifications (covering ~50 minutes), the system stops notifying
4. **Final notice**: On the 5th notification, the alert message includes a nudge: *"This alert has been firing for a while and you haven't acknowledged it. Suppressing further notifications — check the dashboard when you're ready."*
5. **After suppression**: The alert remains visible in the dashboard with status `firing (suppressed)` but generates no further notifications
6. **Spike resolves**: When rate returns below threshold for 2 consecutive evaluation windows, alert transitions to `resolved`
7. **Acknowledged**: User can acknowledge at any time to stop notifications immediately. Status → `acknowledged`
8. **Re-fire**: If the same group spikes again after resolution, a new alert is created (notification counter resets)

Alert lifecycle: `firing` → `firing (suppressed)` → `resolved`
                              ↘ `acknowledged` ↗

---

## 5. Time Windows & Analysis

| Window Type | Options | Used For |
|-------------|---------|----------|
| **Fixed windows** | 5 min, 15 min, 60 min | Spike detection evaluation |
| **Rolling averages** | 1h, 6h, 24h lookback | Baseline calculation |
| **Historical comparison** | Same hour yesterday, same hour last week | Time-of-day normalization |

---

## 6. Architecture

### Technology Stack

| Component | Technology |
|-----------|------------|
| Backend API | .NET 8+ (C#) |
| ML Runtime | ONNX Runtime or ML.NET |
| Embedding Model | all-MiniLM-L6-v2 (or similar, via ONNX) |
| Vector Storage | pgvector extension for PostgreSQL |
| Frontend | React or Vue SPA |
| Database | PostgreSQL (with pgvector) |
| Containerization | Docker (single container MVP) |

### Processing Pipeline Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    Log Jammer Container                   │
│                                                          │
│  ┌─────────────┐    ┌──────────────┐    ┌────────────┐  │
│  │  Scheduler   │───▶│  Adapter     │───▶│  Parser    │  │
│  │  (per source)│    │  (ES/Loki/PG)│    │            │  │
│  └─────────────┘    └──────────────┘    └─────┬──────┘  │
│                                                │         │
│                                          ┌─────▼──────┐  │
│                                          │ Fingerprint │  │
│                                          │ Calculator  │  │
│                                          └─────┬──────┘  │
│                                                │         │
│               ┌────────────────────────────────┤         │
│               │ Known?                         │ New?    │
│               ▼                                ▼         │
│  ┌────────────────┐              ┌──────────────────┐   │
│  │ Update Counts  │              │  ML Classifier   │   │
│  └───────┬────────┘              │  (ONNX Runtime)  │   │
│          │                       └────────┬─────────┘   │
│          │                                │              │
│          └────────────┬───────────────────┘              │
│                       ▼                                  │
│              ┌─────────────────┐                         │
│              │ Spike Detector  │                         │
│              │ (per group)     │                         │
│              └────────┬────────┘                         │
│                       ▼                                  │
│              ┌─────────────────┐    ┌────────────────┐  │
│              │  Alert Manager  │───▶│   REST API     │  │
│              │                 │    │   (Dashboard)  │  │
│              └─────────────────┘    └────────────────┘  │
│                                                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │              PostgreSQL + pgvector                │   │
│  └──────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

### Database Schema (Conceptual)

| Table | Purpose |
|-------|---------|
| `data_sources` | Connection config, poll interval, schema mapping |
| `fingerprint_configs` | Which fields per source compose the fingerprint |
| `known_errors` | The error library (fingerprints, embeddings, tags) |
| `error_occurrences` | Time-bucketed occurrence counts per error group |
| `tags` | Tag definitions (name, type: auto/user, color) |
| `error_tags` | Many-to-many: error groups ↔ tags |
| `alerts` | Alert history with lifecycle state |
| `user_overrides` | Manual classification overrides |
| `classification_queue` | Errors awaiting user review |

---

## 7. User Interface

### Dashboard (MVP)

- **Active Alerts Feed**: Real-time list of firing alerts, sorted by severity
- **Error Groups List**: Browsable, filterable, sortable list of all known error groups
  - Filter by: tags, severity, status, data source, time range
  - Sort by: last seen, occurrence count, severity
- **Error Group Detail View**:
  - Occurrence chart (sparkline over time)
  - Sample log entries (raw JSON, browsable)
  - Tags (with edit capability)
  - Severity and status controls
  - Alert history for this group
- **Correlated Spikes View**: Groups of simultaneously-spiking errors
- **Unclassified Queue**: Errors the system couldn't auto-classify with high confidence
  - Shows suggested tags with confidence scores
  - User confirms, rejects, or manually tags

### Configuration Interface

- **Data Source Management**: Add/edit/test/remove data sources
- **Schema Mapping**: Visual mapping of source fields to Log Jammer fields
- **Fingerprint Configuration**: Select which fields per source compose the fingerprint
- **Spike Thresholds**: Configure detection thresholds per error group or globally
- **Tag Management**: Create/edit/delete tags
- **Sampling Settings**: Configure per-source sampling budgets

---

## 8. MVP Scope

### Included

- Single container deployment (Docker)
- Pull-based ingestion with adaptive sampling
- Local embedding model for classification (ONNX Runtime)
- Auto-enrollment of error groups
- Tag-based classification with user override
- Per-group spike detection (absolute + percentage thresholds)
- Alert deduplication with firing/resolved lifecycle
- Basic cross-group correlation (simultaneous spikes)
- Dashboard with alert feed, error group browser, detail view
- Unclassified error queue for user review
- PostgreSQL with pgvector
- No authentication (internal tool)
- 30-day data retention (library retained indefinitely)

### Excluded from MVP (Future)

- External notifications (Slack, email, PagerDuty)
- Performance/metrics monitoring (Prometheus integration)
- User authentication/authorization
- Multi-tenant support
- Horizontal scaling
- Standard deviation threshold type (requires sufficient baseline data infrastructure)
- Historical comparison baselines (same hour last week)
- Automated root-cause suggestions

---

## 9. Performance Requirements

| Metric | Target |
|--------|--------|
| Poll + process cycle | < 30 seconds end-to-end |
| Embedding generation | < 50ms per error (CPU) |
| Nearest-neighbor search | < 10ms per query (pgvector) |
| Max error groups in memory | 100,000 |
| Concurrent data sources | 10+ |
| No GPU required | Mandatory |
| No remote API calls for ML | Mandatory |

---

## 10. Open Questions

1. **Embedding model final selection**: Benchmark `all-MiniLM-L6-v2` vs `FastText` vs `TF-IDF` for error log similarity quality and CPU performance
2. **pgvector vs in-memory HNSW**: For MVP scale, is pgvector sufficient or do we need an in-memory index?
3. **Dashboard framework**: React vs Vue, charting library (Chart.js vs Recharts vs D3)
4. **Fingerprint field defaults**: What are sensible defaults per adapter type?
5. **Sampling algorithm**: Simple random vs stratified vs reservoir sampling
6. **Model update strategy**: How to handle embedding model upgrades (re-embed all known errors?)
7. **Performance metrics**: When Prometheus integration is added, should it be a data source adapter or a separate subsystem?
