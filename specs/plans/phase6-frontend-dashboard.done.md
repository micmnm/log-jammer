# Phase 6: Frontend - Dashboard

**Status:** draft
**Depends on:** Phase 1 (API skeleton; integrates real APIs from Phases 2-5 as they land)
**Blocks:** Phase 7

---

## Goal

Build the React frontend with MUI + Chart.js, focused on the monitoring dashboard.

---

## Tech Stack

- **React** (Vite)
- **MUI (Material UI)** - component library
- **Chart.js** (react-chartjs-2) - charts
- **React Router** - routing
- **TanStack Query** (React Query) - server state management
- **API client** - auto-generated from OpenAPI spec or manual

---

## Tasks

### 6.1 React project setup
- Create React app with Vite
- Install dependencies: MUI, Chart.js, react-chartjs-2, React Router, TanStack Query
- API client setup (auto-generated from OpenAPI spec or manual typed client)
- Project structure: `pages/`, `components/`, `hooks/`, `api/`

### 6.2 Dashboard layout
- App shell with MUI: sidebar navigation, top bar, main content area
- Responsive layout
- Pages: Dashboard (home), Error Groups, Alerts, Classification Queue, Configuration

### 6.3 Active Alerts Feed
- Real-time list of firing alerts, sorted by severity
- Alert cards with: error group name, severity, spike info, time firing, acknowledge button
- Auto-refresh via polling (MVP; SSE can be added later)
- Correlated spikes grouped together visually

### 6.4 Error Groups List
- Filterable, sortable table (MUI DataGrid)
- Filters: tags, severity, status, data source, time range
- Sort by: last seen, occurrence count, severity
- Inline status/severity badges
- Click to navigate to detail view

### 6.5 Error Group Detail View
- Occurrence chart (Chart.js line chart over time)
- Representative error message + stack trace (code block)
- Sample log entries (browsable, raw JSON expandable)
- Tags (with edit: add/remove tags via chip input)
- Severity and status controls (dropdowns)
- Alert history for this group (table)
- User override controls

### 6.6 Unclassified Queue
- List of errors awaiting review
- Suggested tags with confidence scores (progress bars or percentages)
- Actions: approve suggested tags, reject, manually tag

### 6.7 Backpressure indicator
- Warning banner (MUI Alert) when sampling is active on any data source
- Shows which sources are sampling and at what ratio

### 6.8 Tests
- Component tests (React Testing Library)
- Key user flows: view alerts, browse errors, classify error

---

## Deliverable

Functional dashboard: view alerts, browse/filter error groups, inspect details with charts, classify unclassified errors.

---

## Acceptance Criteria

- [ ] App loads and renders dashboard layout
- [ ] Active alerts feed shows firing alerts sorted by severity
- [ ] Acknowledge button works and updates alert status
- [ ] Error groups list is filterable by tags, severity, status, data source
- [ ] Error groups list is sortable by last seen, count, severity
- [ ] Detail view shows occurrence chart (Chart.js)
- [ ] Detail view shows representative message + stack trace
- [ ] Tags are editable (add/remove) from detail view
- [ ] Classification queue shows unclassified errors with suggested tags
- [ ] Approve/reject/manual tag actions work
- [ ] Backpressure banner appears when sampling is active
- [ ] Auto-refresh updates data without full page reload
