# Elasticsearch Index Discovery UX

## Problem

When adding an Elasticsearch data source, users must manually type index patterns and field names blind. There's no way to browse available indices or see the schema before saving.

## Solution

Add inline discovery to the DataSource dialog for Elasticsearch. After entering connection details, users can browse aliases/data streams, select one to populate the index pattern, and view the schema fields as reference for writing schema mapping JSON.

## Backend: Two New Endpoints

### `POST /api/datasources/discover/indices`

Accepts raw connection config JSON (no saved data source required). Creates a temporary `ElasticsearchClient` and queries for aliases and data streams. Optionally includes concrete indices.

**Request:**
```json
{
  "connectionConfig": "{\"url\":\"http://elk:9200\",\"indexPattern\":\"*\",\"auth\":{...}}",
  "showConcreteIndices": false
}
```

**Response:**
```json
{
  "aliases": [
    { "name": "app-logs", "indices": ["app-logs-2024.01", "app-logs-2024.02"] }
  ],
  "dataStreams": [
    { "name": "logs-nginx-default", "backingIndices": 5 }
  ],
  "concreteIndices": []
}
```

- Default: show aliases + data streams only
- `showConcreteIndices=true`: also list concrete indices
- Backend creates temporary ES client, calls `_cat/aliases`, `_data_stream`, and optionally `_cat/indices`

### `POST /api/datasources/discover/schema`

Accepts raw connection config JSON with an `indexPattern` set to the selected alias/index. Returns the same `SchemaResponse` format already used by `GET /api/datasources/{id}/schema`.

**Request:**
```json
{
  "connectionConfig": "{\"url\":\"http://elk:9200\",\"indexPattern\":\"app-logs\"}"
}
```

**Response:** `{ "fields": [{ "name": "message", "type": "text", "isNullable": true }, ...] }`

Reuses `ElasticsearchAdapter.GetSchemaAsync()` by constructing a temporary adapter from the config.

## Frontend: Inline Discovery in DataSourceDialog

Changes contained within `DataSourceDialog.tsx`:

1. **"Discover" button** next to Index Pattern field - calls `/discover/indices` after user enters URL + auth
2. Results displayed in a selectable list (aliases/data streams shown by default)
3. **Toggle** (off by default): "Show concrete indices" - re-fetches with flag
4. Clicking an item populates the Index Pattern field
5. **"View Schema" button** appears once index pattern is set - calls `/discover/schema`
6. Schema fields shown in a collapsible read-only panel below the schema mapping JSON field

## Implementation Sequence

1. Add DTOs for discover request/response
2. Add `DiscoverIndicesAsync` and `DiscoverSchemaAsync` methods to `ElasticsearchAdapter`
3. Add methods to `IDataSourceService` / `DataSourceService`
4. Add endpoints to `DataSourcesController`
5. Add frontend API hook for discover endpoints
6. Update `DataSourceDialog.tsx` with discover button, index list, toggle, and schema panel
7. Tests for new backend endpoints

## Decisions

- **Aliases by default, concrete indices via toggle** - aliases/data streams are the standard production pattern
- **No visual field mapping dropdowns** - users write schema mapping JSON with schema fields visible as reference
- **Elasticsearch only** - PostgreSQL and LogFile adapters can get discovery later
- **No saved data source required** - discovery works with raw connection config before the data source is created
