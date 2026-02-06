# Log Jammer - MVP Implementation Plan

## Summary of Decisions

| Decision | Choice |
|----------|--------|
| Backend | .NET 8+ / C# |
| Frontend | React + MUI (Material UI) + Chart.js (react-chartjs-2) |
| ML Model | Proposal A: ONNX Runtime + all-MiniLM-L6-v2 (from Phase 1) |
| Database | PostgreSQL + pgvector |
| Deployment | docker-compose (app + PostgreSQL as separate containers) |
| Data Source Adapters (MVP) | Elasticsearch/OpenSearch, PostgreSQL, Log files (JSON lines + plain text/regex) |
| Auth | None (internal tool) |
| Data Retention | 30 days (error library indefinite) |

---

## Phase 1: Foundation & Project Structure

**Goal:** Scaffold the solution, set up database, define core domain models and API skeleton.

### Tasks

1. **Solution structure**
   - Create .NET 8 solution with projects:
     - `LogJammer.Api` - ASP.NET Core Web API (host)
     - `LogJammer.Core` - Domain models, interfaces, shared logic
     - `LogJammer.Infrastructure` - Database, adapters, ML integration
     - `LogJammer.Tests` - Unit + integration tests
   - Set up `docker-compose.yml` (app + PostgreSQL + pgvector)
   - Add `.editorconfig`, `Directory.Build.props` for consistent settings

2. **Database schema & migrations**
   - Install EF Core + Npgsql + pgvector extension
   - Create initial migration with tables:
     - `data_sources` - connection config, poll interval, schema mapping, adapter type
     - `fingerprint_configs` - fields per source that compose the fingerprint
     - `known_errors` - the error library (fingerprint hash, representative message, embedding vector, tags, severity, status, occurrence counts, timestamps)
     - `error_occurrences` - time-bucketed occurrence counts per error group
     - `tags` - tag definitions (name, type: auto/user, color)
     - `error_tags` - many-to-many: error groups to tags
     - `alerts` - alert history with lifecycle state (firing, suppressed, acknowledged, resolved)
     - `user_overrides` - manual classification overrides
     - `classification_queue` - errors awaiting user review
   - Seed default tags (timeout, auth-failure, database, oom, network, etc.)

3. **Core domain models (C# classes)**
   - DTOs matching the database schema
   - Enums: `ErrorSeverity`, `ErrorStatus`, `AlertStatus`, `AdapterType`, `ThresholdType`
   - Interfaces: `IDataSourceAdapter`, `IEmbeddingProvider`, `ISpikeDetector`

4. **API skeleton**
   - Health check endpoint
   - Basic CRUD endpoints structure (empty controllers for data sources, error groups, alerts, tags, configuration)
   - Swagger/OpenAPI setup

5. **Tests**
   - Test project setup with xUnit
   - Database integration test infrastructure (Testcontainers for PostgreSQL)

### Deliverable
Running API with health check, PostgreSQL with schema, docker-compose up works.

---

## Phase 2: Data Source Adapters

**Goal:** Implement the adapter interface and 3 concrete adapters (Elasticsearch, PostgreSQL, Log files).

### Tasks

1. **IDataSourceAdapter interface**
   ```
   - TestConnectionAsync() -> ConnectionTestResult
   - PollErrorsAsync(since, limit) -> ErrorBatch
   - GetSampleRecordsAsync(count) -> RawLogEntry[]
   - GetSchemaAsync() -> FieldDefinition[]
   ```

2. **Elasticsearch/OpenSearch adapter**
   - NuGet: `Elastic.Clients.Elasticsearch` (or `NEST` for OpenSearch compat)
   - Configurable: URL, index pattern, auth (basic/API key), query filter
   - Poll via search API with timestamp range
   - Schema discovery from index mapping

3. **PostgreSQL adapter**
   - Direct SQL via Npgsql (or EF Core raw queries)
   - Configurable: connection string, table name, timestamp column, query filter
   - Schema discovery from `information_schema`

4. **Log file adapter**
   - Tail local files or watch a directory
   - Two parsing modes:
     - **JSON lines**: Each line is a JSON object, fields extracted directly
     - **Plain text + regex**: User-configurable regex with named capture groups (e.g., `(?<timestamp>...) (?<level>...) (?<message>...)`)
   - Track file position (offset) to avoid re-reading
   - Support file rotation (detect new file, reset offset)

5. **Data source management API**
   - CRUD for data sources
   - Test connection endpoint
   - Get schema / sample records endpoints

6. **Adapter factory & registration**
   - `IDataSourceAdapterFactory` resolves adapter by `AdapterType`
   - DI registration for all adapters

7. **Tests**
   - Unit tests per adapter (mocked HTTP/file system)
   - Integration test with real Elasticsearch (Testcontainers)

### Deliverable
Can configure data sources via API, test connections, pull sample records from ES, PostgreSQL, and log files.

---

## Phase 3: Error Processing Pipeline (Fingerprint + Occurrence Tracking)

**Goal:** Implement the core processing loop: poll -> parse -> fingerprint -> store/update occurrences.

### Tasks

1. **Schema mapping engine**
   - Map source fields to Log Jammer internal model (message, timestamp, severity, stack_trace, custom fields)
   - Configurable per data source via the `fingerprint_configs` table

2. **Fingerprint calculator**
   - Normalize error text: strip line numbers, memory addresses, timestamps, request IDs, UUIDs
   - Compute SHA-256 hash from configured fields (normalized)
   - Look up against known error library

3. **Processing pipeline (background service)**
   - `IHostedService` / `BackgroundService` per active data source
   - Scheduler: poll at configured interval (default 30s)
   - Pipeline steps: Poll -> Parse -> Fingerprint -> (Known? Update count : Queue for classification) -> Update time-window aggregates
   - Adaptive sampling: when poll returns > budget (default 500), prioritize diversity + random sample, record sample ratio

4. **Occurrence tracking**
   - Time-bucketed counts in `error_occurrences` (5m, 15m, 1h windows)
   - Rolling window updates
   - Extrapolate counts when sampling is active

5. **Known error library auto-enrollment**
   - New fingerprint -> create `known_errors` record with status `active`
   - Store representative message + stack trace

6. **Data retention job**
   - Background job (daily): prune occurrence data older than 30 days
   - Keep error library entries indefinitely (just prune occurrence windows)

7. **Backpressure monitoring**
   - Track when sampling is active per data source
   - Expose via API for dashboard warning

8. **Tests**
   - Fingerprint normalization tests (strip line numbers, UUIDs, etc.)
   - Pipeline integration tests
   - Sampling logic tests

### Deliverable
Background services polling configured data sources, fingerprinting errors, building the known error library, tracking occurrences per time window.

---

## Phase 4: ML Classification (Embeddings + Auto-Tagging)

**Goal:** Integrate ONNX Runtime + all-MiniLM-L6-v2 for semantic classification of new errors.

### Tasks

1. **IEmbeddingProvider interface + ONNX implementation**
   - Interface: `GenerateEmbeddingAsync(text) -> float[]`, `ComputeSimilarity(a, b) -> float`
   - Export all-MiniLM-L6-v2 + tokenizer to ONNX format
   - NuGet: `Microsoft.ML.OnnxRuntime`
   - Load model on startup, generate 384-dim embeddings
   - Benchmark: must be < 50ms per embedding on CPU

2. **pgvector integration**
   - Store embedding vectors in `known_errors.embedding_vector` column
   - Nearest-neighbor search via pgvector (cosine similarity)
   - Index: IVFFlat or HNSW on embedding column

3. **Classification pipeline (Stage 3 from requirements)**
   - For new fingerprints:
     1. Generate embedding for error message + stack trace
     2. Nearest-neighbor search against existing error group embeddings
     3. Similarity threshold (configurable, default cosine > 0.85):
        - Above: suggest merge into existing group, inherit tags
        - Below: create new error group
     4. Auto-tag via tag centroids (average embedding per tag)
     5. Low-confidence -> add to classification queue

4. **Tag centroid management**
   - Compute and cache average embedding per tag
   - Recalculate when user corrects classifications
   - Store centroids in database

5. **Classification queue**
   - Errors the system couldn't classify with high confidence
   - API endpoints: list queue, approve/reject suggested tags, manual tag assignment

6. **User-driven learning loop**
   - When user corrects tags: update error group, recalculate tag centroid
   - Override system: tag override, severity override, status override, classification pin

7. **Tests**
   - Embedding generation tests (model loads, produces 384-dim vectors)
   - Similarity calculation tests
   - Classification pipeline integration tests
   - Tag centroid calculation tests

### Deliverable
New errors are automatically embedded, classified via nearest-neighbor, auto-tagged. Unclassified errors go to review queue. Users can correct and the system learns.

---

## Phase 5: Spike Detection & Alert System

**Goal:** Detect spikes per error group and manage alert lifecycle.

### Tasks

1. **Spike detection engine**
   - Per-group evaluation on each processing cycle
   - Threshold types (MVP):
     - **Absolute**: count exceeds N in window
     - **Percentage increase**: rate increases by X% vs. rolling average baseline
   - Baseline calculation: rolling average over configurable lookback (default 24h)
   - Cold start: absolute thresholds only until 24h of data exists

2. **Alert manager**
   - Create alert when spike detected
   - Alert lifecycle: `firing` -> `firing (suppressed)` -> `resolved`
   - Capped escalation: max 1 reminder per 10 min, max 5 notifications total
   - Auto-resolve: rate below threshold for 2 consecutive evaluation windows
   - User acknowledge: stops notifications immediately
   - Deduplication: one active alert per error group

3. **Cross-group correlation (basic)**
   - If 3+ groups from same data source spike within same 5-min window -> correlated spike alert
   - Show common attributes across correlated groups

4. **Spike detection configuration API**
   - CRUD for detection rules (per error group or global defaults)
   - Configure thresholds, windows, lookback periods

5. **Alert API**
   - List active alerts (with filtering)
   - Acknowledge alert
   - Alert history

6. **Tests**
   - Spike detection unit tests (various threshold scenarios)
   - Alert lifecycle state machine tests
   - Cross-group correlation tests

### Deliverable
System detects error spikes, manages alert lifecycle with capped escalation, supports correlated spike detection.

---

## Phase 6: Frontend - Dashboard

**Goal:** Build the React frontend with MUI, focused on the monitoring dashboard.

### Tasks

1. **React project setup**
   - Create React app (Vite)
   - Install MUI, Chart.js (react-chartjs-2), React Router, React Query (TanStack Query)
   - API client setup (auto-generated from OpenAPI spec or manual)
   - Project structure: pages, components, hooks, api

2. **Dashboard layout**
   - App shell with MUI: sidebar navigation, top bar, main content area
   - Responsive layout

3. **Active Alerts Feed**
   - Real-time list of firing alerts, sorted by severity
   - Alert cards with: error group name, severity, spike info, time firing, acknowledge button
   - Auto-refresh (polling or SSE)
   - Correlated spikes grouped together

4. **Error Groups List**
   - Filterable, sortable table (MUI DataGrid)
   - Filters: tags, severity, status, data source, time range
   - Sort by: last seen, occurrence count, severity
   - Inline status/severity badges

5. **Error Group Detail View**
   - Occurrence chart (Chart.js line chart over time)
   - Representative error message + stack trace
   - Sample log entries (browsable, raw JSON)
   - Tags (with edit: add/remove tags)
   - Severity and status controls
   - Alert history for this group
   - User override controls

6. **Unclassified Queue**
   - List of errors awaiting review
   - Suggested tags with confidence scores
   - Approve / reject / manually tag actions

7. **Backpressure indicator**
   - Warning banner when sampling is active on any data source

8. **Tests**
   - Component tests (React Testing Library)
   - Key user flows: view alerts, browse errors, classify error

### Deliverable
Functional dashboard: view alerts, browse/filter error groups, inspect details, classify unclassified errors.

---

## Phase 7: Frontend - Configuration

**Goal:** Build the configuration UI for managing data sources, rules, and tags.

### Tasks

1. **Data Source Management**
   - List configured data sources with connection status
   - Add/edit/remove data source wizard:
     - Adapter type selection
     - Connection details form (differs per adapter)
     - Test connection button
     - Schema discovery + field preview
   - Poll interval configuration

2. **Schema Mapping**
   - Visual field mapping: source field -> Log Jammer field (message, timestamp, severity, stack_trace)
   - Preview with sample records from the data source

3. **Fingerprint Configuration**
   - Select which fields per source compose the fingerprint
   - Preview: show how sample records would be fingerprinted

4. **Spike Threshold Configuration**
   - Global default thresholds
   - Per-error-group threshold overrides
   - Configuration for: threshold type, value, time window, lookback period

5. **Tag Management**
   - Create/edit/delete tags
   - Tag name, color, type (auto/user)

6. **Sampling Settings**
   - Per-source poll budget configuration
   - Sampling strategy display

7. **Tests**
   - Configuration form validation tests
   - Data source wizard flow tests

### Deliverable
Full configuration UI: manage data sources with test connection, map schemas, configure fingerprints, set up spike thresholds, manage tags.

---

## Phase 8: Docker, Integration & Polish

**Goal:** Containerize, end-to-end test, performance validate, documentation.

### Tasks

1. **Dockerfile**
   - Multi-stage build: build .NET app + build React app -> slim runtime image
   - Bundle ONNX model in the image
   - Health check in Dockerfile

2. **docker-compose.yml (production-ready)**
   - App container
   - PostgreSQL container (with pgvector extension)
   - Volume for PostgreSQL data
   - Volume for log files (if using log file adapter)
   - Environment variable configuration
   - Networking

3. **End-to-end testing**
   - Spin up full stack via docker-compose
   - Configure a data source, ingest sample data
   - Verify: fingerprinting, classification, spike detection, alerts, dashboard rendering

4. **Performance validation**
   - Load test: 10k+ logs/minute ingestion
   - Verify < 30s end-to-end processing
   - Verify embedding generation < 50ms
   - Memory profiling under load

5. **Error handling & resilience**
   - Adapter connection retry logic
   - Graceful handling of data source unavailability
   - Processing pipeline error isolation (one source failing doesn't block others)

6. **API documentation**
   - Swagger/OpenAPI spec finalized
   - Update `definition-api.md` and `definition-dto.md`

7. **README & getting started guide**
   - How to run with docker-compose
   - How to configure your first data source
   - Environment variables reference

### Deliverable
Production-ready MVP: `docker-compose up` starts the full stack, configured and ready to monitor.

---

## Phase Summary

| Phase | Name | Key Outcome |
|-------|------|-------------|
| 1 | Foundation | Solution scaffold, DB schema, API skeleton, docker-compose |
| 2 | Data Source Adapters | ES, PostgreSQL, Log file adapters + management API |
| 3 | Processing Pipeline | Poll -> parse -> fingerprint -> track occurrences |
| 4 | ML Classification | ONNX embeddings, auto-tagging, classification queue |
| 5 | Spike Detection | Per-group spike detection, alert lifecycle, correlation |
| 6 | Frontend - Dashboard | Alerts feed, error groups, detail view, classification UI |
| 7 | Frontend - Configuration | Data source wizard, schema mapping, threshold config |
| 8 | Docker & Integration | Containerization, E2E tests, performance validation |

### Dependencies

```
Phase 1 ──> Phase 2 ──> Phase 3 ──> Phase 5
                              │
                              └──> Phase 4

Phase 1 ──> Phase 6 (can start after API skeleton exists)
Phase 6 ──> Phase 7

Phase 5 + Phase 7 ──> Phase 8
```

Phases 4 and 5 can be worked on in parallel (both depend on Phase 3).
Phase 6 can start as soon as Phase 1's API skeleton exists (using mock data initially), then integrate real APIs as phases 2-5 deliver them.

---

## Open Items / Risks

1. **Tokenizer for ONNX**: Need to resolve how to handle the tokenizer for all-MiniLM-L6-v2 in .NET (AllMiniLmL6V2Sharp vs custom ONNX export). Should be prototyped early in Phase 4.
2. **Log file adapter complexity**: File rotation, position tracking, and regex parsing add significant complexity. May want to simplify for MVP (JSON lines only, add regex later).
3. **Real-time dashboard updates**: Polling vs SSE vs WebSocket for the frontend. Polling is simplest for MVP; SSE can be added later.
4. **pgvector performance at scale**: Need to validate nearest-neighbor search performance with 100K vectors. Should benchmark in Phase 4.
