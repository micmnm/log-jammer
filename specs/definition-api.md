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
| GET | `/api/alerts` | List all alerts | Skeleton (501) |
| GET | `/api/alerts/{id}` | Get alert by ID | Skeleton (501) |
| POST | `/api/alerts/{id}/acknowledge` | Acknowledge an alert | Skeleton (501) |

## Tags

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/tags` | List all tags | Skeleton (501) |
| POST | `/api/tags` | Create a tag | Skeleton (501) |
| PUT | `/api/tags/{id}` | Update a tag | Skeleton (501) |
| DELETE | `/api/tags/{id}` | Delete a tag | Skeleton (501) |

## Configuration

| Method | Path | Description | Status |
|--------|------|-------------|--------|
| GET | `/api/configuration` | Get app configuration | Skeleton (501) |
| PUT | `/api/configuration` | Update app configuration | Skeleton (501) |

## OpenAPI

| Path | Description |
|------|-------------|
| `/openapi/v1.json` | OpenAPI 3.0 specification |
| `/scalar/v1` | Scalar API reference UI |
