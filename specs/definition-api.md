# Definition API

Base URL: `http://localhost:5000`

## Health

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/health` | Application health check | Implemented |
| GET | `/healthz` | Infrastructure health check (DB) | Implemented |

## Data Sources

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/datasources` | List all data sources | Implemented |
| GET | `/api/datasources/{id}` | Get data source by ID | Implemented |
| POST | `/api/datasources` | Create data source | Implemented |
| PUT | `/api/datasources/{id}` | Update data source | Implemented |
| DELETE | `/api/datasources/{id}` | Delete data source | Implemented |
| POST | `/api/datasources/{id}/test` | Test data source connection | Implemented |
| GET | `/api/datasources/{id}/schema` | Get data source schema | Implemented |
| GET | `/api/datasources/{id}/sample` | Get sample records (query: count) | Implemented |

## Error Groups

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/errorgroups` | List error groups (query: dataSourceId, status, severity, page, pageSize) | Implemented |
| GET | `/api/errorgroups/{id}` | Get error group detail by ID | Implemented |
| GET | `/api/errorgroups/{id}/occurrences` | Get occurrence history (query: from, to) | Implemented |
| PUT | `/api/errorgroups/{id}/status` | Update error group status | Implemented |
| PUT | `/api/errorgroups/{id}/severity` | Update error group severity | Implemented |

## Alerts

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/alerts` | List alerts (query: status, dataSourceId, page, pageSize) | Implemented |
| GET | `/api/alerts/{id}` | Get alert by ID | Implemented |
| POST | `/api/alerts/{id}/acknowledge` | Acknowledge an alert | Implemented |
| GET | `/api/alerts/history` | List resolved alerts (query: dataSourceId, page, pageSize) | Implemented |
| GET | `/api/alerts/correlated` | List correlated spike alerts (query: status, page, pageSize) | Implemented |

## Fingerprint Configs

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/datasources/{dataSourceId}/fingerprint-configs` | List fingerprint configs for data source | Implemented |
| POST | `/api/datasources/{dataSourceId}/fingerprint-configs` | Create fingerprint config | Implemented |
| GET | `/api/datasources/{dataSourceId}/fingerprint-configs/{id}` | Get fingerprint config by ID | Implemented |
| PUT | `/api/datasources/{dataSourceId}/fingerprint-configs/{id}` | Update fingerprint config | Implemented |
| DELETE | `/api/datasources/{dataSourceId}/fingerprint-configs/{id}` | Delete fingerprint config | Implemented |

## Spike Detection Rules

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/spikedetectionrules` | List all rules | Implemented |
| GET | `/api/spikedetectionrules/{id}` | Get rule by ID | Implemented |
| POST | `/api/spikedetectionrules` | Create a rule | Implemented |
| PUT | `/api/spikedetectionrules/{id}` | Update a rule | Implemented |
| DELETE | `/api/spikedetectionrules/{id}` | Delete a rule | Implemented |

## Tags

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/tags` | List all tags | Implemented |
| GET | `/api/tags/{id}` | Get tag by ID | Implemented |
| POST | `/api/tags` | Create a tag | Implemented |
| PUT | `/api/tags/{id}` | Update a tag | Implemented |
| DELETE | `/api/tags/{id}` | Delete a tag | Implemented |

## Configuration

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/configuration` | Get all classification config key/values | Implemented |
| PUT | `/api/configuration` | Update a configuration value | Implemented |

## Classification Queue

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/classification/queue` | List pending classification items (query: page, pageSize) | Implemented |
| GET | `/api/classification/queue/{id}` | Get single classification queue item | Implemented |
| POST | `/api/classification/queue/{id}/approve` | Accept suggested tags | Implemented |
| POST | `/api/classification/queue/{id}/reject` | Reject with user-provided tags | Implemented |

## CORS

| Origin | Methods | Status |
|--------|---------|--------|
| `http://localhost:5173` | All | Implemented (dev policy) |

## OpenAPI

| Path | Description |
|------|-------------|
| `/openapi/v1.json` | OpenAPI 3.0 specification |
| `/scalar/v1` | Scalar API reference UI |
