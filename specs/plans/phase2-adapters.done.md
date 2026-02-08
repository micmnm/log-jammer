# Phase 2: Data Source Adapters

**Status:** draft
**Depends on:** Phase 1
**Blocks:** Phase 3

---

## Goal

Implement the adapter interface and 3 concrete adapters (Elasticsearch/OpenSearch, PostgreSQL, Log files).

---

## Tasks

### 2.1 IDataSourceAdapter interface
```
- TestConnectionAsync() -> ConnectionTestResult
- PollErrorsAsync(since, limit) -> ErrorBatch
- GetSampleRecordsAsync(count) -> RawLogEntry[]
- GetSchemaAsync() -> FieldDefinition[]
```

### 2.2 Elasticsearch/OpenSearch adapter
- NuGet: `Elastic.Clients.Elasticsearch` (or `NEST` for OpenSearch compat)
- Configurable: URL, index pattern, auth (basic/API key), query filter
- Poll via search API with timestamp range
- Schema discovery from index mapping

### 2.3 PostgreSQL adapter
- Direct SQL via Npgsql (or EF Core raw queries)
- Configurable: connection string, table name, timestamp column, query filter
- Schema discovery from `information_schema`

### 2.4 Log file adapter
- Tail local files or watch a directory
- Two parsing modes:
  - **JSON lines**: Each line is a JSON object, fields extracted directly
  - **Plain text + regex**: User-configurable regex with named capture groups (e.g., `(?<timestamp>...) (?<level>...) (?<message>...)`)
- Track file position (offset) to avoid re-reading
- Support file rotation (detect new file, reset offset)

### 2.5 Data source management API
- CRUD for data sources
- Test connection endpoint
- Get schema / sample records endpoints

### 2.6 Adapter factory & registration
- `IDataSourceAdapterFactory` resolves adapter by `AdapterType`
- DI registration for all adapters

### 2.7 Tests
- Unit tests per adapter (mocked HTTP/file system)
- Integration test with real Elasticsearch (Testcontainers)

---

## Deliverable

Can configure data sources via API, test connections, pull sample records from ES, PostgreSQL, and log files.

---

## Acceptance Criteria

- [ ] CRUD API for data sources works (create, read, update, delete)
- [ ] Test connection endpoint verifies connectivity per adapter type
- [ ] Elasticsearch adapter polls records from a real ES instance (integration test)
- [ ] PostgreSQL adapter reads from a log table
- [ ] Log file adapter reads JSON lines files
- [ ] Log file adapter parses plain text logs with configurable regex
- [ ] File position tracking persists across polls (no duplicate reads)
- [ ] Schema discovery returns field definitions for each adapter
- [ ] Sample records endpoint returns formatted log entries
