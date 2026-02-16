# SampleLog → LogJammer Integration

**Date:** 2026-02-16
**Status:** Draft — approved design, pending implementation plan

## Goal

Make it easy to ingest SampleLog-generated logs into LogJammer. Two parts:
1. **General:** Auto-detect log format and propose field mappings when adding a LogFile data source
2. **SampleLog convenience:** One-click registration from the SampleLog TUI

## Design

### 1. SampleLog Output Changes

Replace current CLEF JSON + prebaked raw text with:

- **`sample.json`** — ELK-style JSON lines:
  ```json
  {"timestamp":"2026-02-16T12:34:56.123Z","level":"ERROR","message":"Failed to connect to database","service":"MyApp.DataService","traceId":"abc123","duration":1200}
  ```
- **`sample.log`** — Simple text:
  ```
  2026-02-16 12:34:56.123 ERROR Failed to connect to database
  ```

Both generated from the same scenario data, same events. Output directory changes to `{repo}/logs/` (not `src/SampleLog/logs/`) to align with Docker volume mount. Directory is gitignored (runtime mock data).

### 2. Detect Endpoint

**`POST /api/datasources/detect`**

Request:
```json
{ "filePath": "/app/logs/sample.json" }
```

Response:
```json
{
  "detectedFormat": "jsonlines",
  "fields": [
    { "name": "timestamp", "type": "DateTime", "proposedRole": "Timestamp" },
    { "name": "level", "type": "String", "proposedRole": "Level" },
    { "name": "message", "type": "String", "proposedRole": "Message" },
    { "name": "service", "type": "String", "proposedRole": null }
  ],
  "sampleRecords": [ ... ],
  "proposedConfig": {
    "filePath": "/app/logs/sample.json",
    "parseMode": "jsonlines",
    "timestampField": "timestamp"
  }
}
```

Detection logic (`LogFileDetectService`):
- Read up to 200 lines for JSON (field discovery across varied entries), 20 lines for text
- JSON detection: try parse each line, >80% success → jsonlines
- Text detection: try known regex patterns (simple timestamp+level: `^(?<timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\.\d+)\s+(?<level>\w+)\s+(?<message>.+)$`)
- Field role inference: match field names against known patterns (`timestamp`/`@t`/`time`/`date` → Timestamp, `level`/`@l`/`severity` → Level, `message`/`@mt`/`msg` → Message)
- Return 5 sample parsed records for preview
- Security: validate path under allowed directories, reject `..` traversal

### 3. LogFileConnectionConfig Fix

Change from `FilePaths` (string array) to `FilePath` (single string). One file per data source.

Add fields:
- `LevelField` (string?) — which field holds the log level
- `MessageField` (string?) — which field holds the message

### 4. Frontend DataSourceDialog Improvements

When adapter type is LogFile:

1. **File Path** — text field
2. **"Detect" button** — calls `POST /api/datasources/detect`
3. After detect, auto-fill:
   - **Parse Mode** — dropdown (jsonlines/text), editable
   - **Timestamp Field** — auto-filled, editable
   - **Level Field** — auto-filled, editable
   - **Message Field** — auto-filled, editable
   - **Regex Pattern** — shown only when parse mode is text
   - **Sample Preview** — table with 5 parsed records
4. **Test Connection available before save** (fix edit-only restriction)

**Validation — save disabled until:**
- File path filled
- Detect ran successfully
- Timestamp, Level, Message fields filled
- Parse Mode set
- If text: Regex Pattern filled

### 5. SampleLog TUI Registration

New shortcut `[R] Register with LogJammer`:
- Prompts: `[1] JSON` / `[2] Text` / `[3] Both`
- Calls detect endpoint → then creates data source via API
- Shows success/failure in status bar
- API base URL configurable in `appsettings.json` (default `http://localhost:5050`)

### 6. Docker Path Alignment

SampleLog writes to `{repo}/logs/`. Docker compose already mounts `./logs:/app/logs`. Paths align without changes to compose config.

SampleLog `[R]` Register uses local absolute paths (SampleLog runs on host, not in container).

### 7. Testing

**Backend:**
- `LogFileDetectService` unit tests: JSON detection, text detection, field role inference, mixed/malformed content, path traversal rejection
- Updated `LogFileAdapter` tests for single `FilePath`
- Detect endpoint integration test

**Frontend:**
- DataSourceDialog test: Detect button, auto-fill, validation, preview

**SampleLog:**
- JSON output validates as JSON lines with expected fields
- Text output matches timestamp+level+message pattern
