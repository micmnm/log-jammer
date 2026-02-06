# Phase 7: Frontend - Configuration

**Status:** draft
**Depends on:** Phase 6
**Blocks:** Phase 8

---

## Goal

Build the configuration UI for managing data sources, detection rules, and tags.

---

## Tasks

### 7.1 Data Source Management
- List configured data sources with connection status indicator (green/red)
- Add/edit/remove data source wizard (multi-step form):
  1. Adapter type selection (Elasticsearch, PostgreSQL, Log files)
  2. Connection details form (fields differ per adapter type)
  3. Test connection button with result feedback
  4. Schema discovery + field preview table
- Poll interval configuration (slider or input)

### 7.2 Schema Mapping
- Visual field mapping UI: source field → Log Jammer field (message, timestamp, severity, stack_trace)
- Drag-and-drop or dropdown selectors
- Preview with sample records fetched from the data source
- Validation: required fields must be mapped

### 7.3 Fingerprint Configuration
- Select which fields per source compose the fingerprint (checkbox list)
- Preview panel: show how sample records would be fingerprinted
- Show resulting fingerprint hash for each sample

### 7.4 Spike Threshold Configuration
- Global default thresholds (form with threshold type, value, window)
- Per-error-group threshold overrides (accessible from error group detail)
- Configuration for: threshold type (absolute/percentage), value, time window, lookback period

### 7.5 Tag Management
- Table of all tags with: name, color swatch, type (auto/user), usage count
- Create new tag (name, color picker, type)
- Edit / delete existing tags
- Prevent deletion of tags in use (or warn)

### 7.6 Sampling Settings
- Per-source poll budget configuration (number input)
- Display current sampling status per source

### 7.7 Tests
- Configuration form validation tests
- Data source wizard multi-step flow tests
- Schema mapping interaction tests

---

## Deliverable

Full configuration UI: manage data sources with test connection, map schemas visually, configure fingerprints, set spike thresholds, manage tags.

---

## Acceptance Criteria

- [ ] Data source list shows all sources with connection status
- [ ] Add data source wizard works for all 3 adapter types
- [ ] Test connection shows success/failure feedback
- [ ] Schema discovery displays fields from the source
- [ ] Schema mapping UI maps source fields to internal fields
- [ ] Sample record preview updates based on mapping
- [ ] Fingerprint field selection shows preview hash
- [ ] Global spike thresholds are configurable
- [ ] Per-group threshold overrides work
- [ ] Tags can be created, edited, and deleted
- [ ] Sampling budget is configurable per source
