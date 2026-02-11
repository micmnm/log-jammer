# Definition DTO

## Enums

### ErrorSeverity
`LogJammer.Core.Enums.ErrorSeverity`
- `Info`
- `Warning`
- `Critical`

### ErrorStatus
`LogJammer.Core.Enums.ErrorStatus`
- `Active`
- `Resolved`
- `Ignored`
- `Expected`

### AlertStatus
`LogJammer.Core.Enums.AlertStatus`
- `Firing`
- `FiringSuppressed`
- `Acknowledged`
- `Resolved`

### AdapterType
`LogJammer.Core.Enums.AdapterType`
- `Elasticsearch`
- `LogFile`
- `PostgreSql`

### ThresholdType
`LogJammer.Core.Enums.ThresholdType`
- `Absolute`
- `PercentageIncrease`
- `StandardDeviation`

---

## Entities

### DataSource
`LogJammer.Core.Entities.DataSource` → `data_sources`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK, auto-generated |
| Name | string | name | max 200 |
| AdapterType | AdapterType | adapter_type | stored as string |
| ConnectionConfig | string | connection_config | jsonb |
| PollIntervalSeconds | int | poll_interval_seconds | default 30 |
| SchemaMapping | string? | schema_mapping | jsonb |
| SamplingBudget | int | sampling_budget | default 500 |
| Enabled | bool | enabled | default true |
| CreatedAt | DateTime | created_at | auto-set |
| UpdatedAt | DateTime | updated_at | auto-set |

### FingerprintConfig
`LogJammer.Core.Entities.FingerprintConfig` → `fingerprint_configs`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK |
| DataSourceId | Guid | data_source_id | FK → data_sources |
| FieldName | string | field_name | max 200 |
| Order | int | order | |
| NormalizeBeforeHash | bool | normalize_before_hash | default true |
| CreatedAt | DateTime | created_at | auto-set |

### KnownError
`LogJammer.Core.Entities.KnownError` → `known_errors`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK |
| FingerprintHash | string | fingerprint_hash | max 64, unique index |
| RepresentativeMessage | string | representative_message | |
| RepresentativeStackTrace | string? | representative_stack_trace | |
| EmbeddingVector | Vector? | embedding_vector | vector(384) |
| Severity | ErrorSeverity | severity | stored as string |
| Status | ErrorStatus | status | stored as string |
| FirstSeen | DateTime | first_seen | |
| LastSeen | DateTime | last_seen | |
| TotalOccurrences | long | total_occurrences | |
| OccurrenceWindows | string? | occurrence_windows | jsonb |
| DataSourceId | Guid | data_source_id | FK → data_sources |
| CreatedAt | DateTime | created_at | auto-set |
| UpdatedAt | DateTime | updated_at | auto-set |

### ErrorOccurrence
`LogJammer.Core.Entities.ErrorOccurrence` → `error_occurrences`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK |
| KnownErrorId | Guid | known_error_id | FK → known_errors, composite index with WindowStart |
| WindowStart | DateTime | window_start | |
| WindowEnd | DateTime | window_end | |
| Count | long | count | |
| SampleRatio | double? | sample_ratio | |
| CreatedAt | DateTime | created_at | auto-set |

### Tag
`LogJammer.Core.Entities.Tag` → `tags`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK |
| Name | string | name | max 100, unique index |
| TagType | string | tag_type | "auto" or "user", max 20 |
| Color | string? | color | max 7 (hex) |
| CreatedAt | DateTime | created_at | auto-set |

### ErrorTag
`LogJammer.Core.Entities.ErrorTag` → `error_tags`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| KnownErrorId | Guid | known_error_id | Composite PK, FK → known_errors |
| TagId | Guid | tag_id | Composite PK, FK → tags |
| IsAutoAssigned | bool | is_auto_assigned | |
| Confidence | double? | confidence | |
| CreatedAt | DateTime | created_at | auto-set |

### Alert
`LogJammer.Core.Entities.Alert` → `alerts`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK |
| KnownErrorId | Guid | known_error_id | FK → known_errors |
| Status | AlertStatus | status | stored as string |
| ThresholdType | ThresholdType | threshold_type | stored as string |
| ThresholdValue | double | threshold_value | |
| ActualValue | double | actual_value | |
| NotificationCount | int | notification_count | |
| LastNotifiedAt | DateTime? | last_notified_at | |
| AcknowledgedAt | DateTime? | acknowledged_at | |
| ResolvedAt | DateTime? | resolved_at | |
| CreatedAt | DateTime | created_at | auto-set |
| UpdatedAt | DateTime | updated_at | auto-set |

### UserOverride
`LogJammer.Core.Entities.UserOverride` → `user_overrides`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK |
| KnownErrorId | Guid | known_error_id | FK → known_errors |
| OverrideType | string | override_type | tag/severity/status/fingerprint/classification |
| OverrideData | string | override_data | jsonb |
| Reason | string? | reason | max 500 |
| CreatedAt | DateTime | created_at | auto-set |

### ClassificationQueueItem
`LogJammer.Core.Entities.ClassificationQueueItem` → `classification_queue`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK |
| KnownErrorId | Guid | known_error_id | FK → known_errors |
| SuggestedTags | string? | suggested_tags | jsonb |
| Confidence | double? | confidence | |
| Reviewed | bool | reviewed | partial index where false |
| CreatedAt | DateTime | created_at | auto-set |
| ReviewedAt | DateTime? | reviewed_at | |

---

## Models (Pipeline)

### MappedLogEntry
`LogJammer.Core.Models.MappedLogEntry`
- `Message` → `string`
- `Timestamp` → `DateTime`
- `Severity` → `ErrorSeverity?`
- `StackTrace` → `string?`
- `CustomFields` → `Dictionary<string, object?>`

---

## Interfaces

### ISchemaMapper
`LogJammer.Core.Interfaces.ISchemaMapper`
- `Map(entry, schemaMappingJson?)` → `MappedLogEntry`

### IFingerprintCalculator
`LogJammer.Core.Interfaces.IFingerprintCalculator`
- `ComputeFingerprint(entry, configs)` → `string`

### IKnownErrorRepository
`LogJammer.Core.Interfaces.IKnownErrorRepository`
- `GetByFingerprintHashAsync(fingerprintHash)` → `Task<KnownError?>`
- `GetAllAsync(dataSourceId?, status?, severity?, page, pageSize)` → `Task<IReadOnlyList<KnownError>>`
- `GetCountAsync(dataSourceId?, status?, severity?)` → `Task<int>`
- `GetByIdAsync(id)` → `Task<KnownError?>`
- `AddAsync(knownError)` → `Task<KnownError>`
- `UpdateAsync(knownError)` → `Task`

### IErrorOccurrenceRepository
`LogJammer.Core.Interfaces.IErrorOccurrenceRepository`
- `UpsertWindowAsync(knownErrorId, windowStart, windowEnd, sampleRatio?)` → `Task`
- `GetByKnownErrorAsync(knownErrorId, from?, to?)` → `Task<IReadOnlyList<ErrorOccurrence>>`
- `DeleteOlderThanAsync(cutoff)` → `Task<int>`

### IDataSourceAdapter
`LogJammer.Core.Interfaces.IDataSourceAdapter`
- `TestConnectionAsync()` → `Task<ConnectionTestResult>`
- `PollErrorsAsync(since, limit)` → `Task<ErrorBatch>`
- `GetSampleRecordsAsync(count)` → `Task<IReadOnlyList<RawLogEntry>>`
- `GetSchemaAsync()` → `Task<IReadOnlyList<FieldDefinition>>`

### IDataSourceAdapterFactory
`LogJammer.Core.Interfaces.IDataSourceAdapterFactory`
- `CreateAdapter(adapterType, connectionConfig)` → `IDataSourceAdapter`

### IDataSourceRepository
`LogJammer.Core.Interfaces.IDataSourceRepository`
- `GetAllAsync()` → `Task<IReadOnlyList<DataSource>>`
- `GetByIdAsync(id)` → `Task<DataSource?>`
- `AddAsync(dataSource)` → `Task<DataSource>`
- `UpdateAsync(dataSource)` → `Task`
- `DeleteAsync(dataSource)` → `Task`
- `ExistsAsync(id)` → `Task<bool>`

### IEmbeddingProvider
`LogJammer.Core.Interfaces.IEmbeddingProvider`
- `GenerateEmbeddingAsync(text)` → `Task<float[]>`
- `GenerateEmbeddingsAsync(texts)` → `Task<IReadOnlyList<float[]>>`
- `Dimensions` → `int` (384 for all-MiniLM-L6-v2)

### ISpikeDetector
`LogJammer.Core.Interfaces.ISpikeDetector`
- `EvaluateAsync(knownErrorId)` → `Task<SpikeResult?>`

---

## Models (Records)

### ErrorBatch
`LogJammer.Core.Models.ErrorBatch`
- `Entries` → `IReadOnlyList<RawLogEntry>`
- `TotalAvailable` → `int`
- `SampleRatio` → `double`

### RawLogEntry
`LogJammer.Core.Models.RawLogEntry`
- `Timestamp` → `DateTime`
- `Fields` → `Dictionary<string, object?>`

### FieldDefinition
`LogJammer.Core.Models.FieldDefinition`
- `Name` → `string`
- `Type` → `string`
- `IsNullable` → `bool`

### ConnectionTestResult
`LogJammer.Core.Models.ConnectionTestResult`
- `Success` → `bool`
- `ErrorMessage` → `string?`
- `Latency` → `TimeSpan`
- `Metadata` → `Dictionary<string, object?>?`

### SpikeResult
`LogJammer.Core.Models.SpikeResult`
- `KnownErrorId` → `Guid`
- `ThresholdType` → `ThresholdType`
- `ThresholdValue` → `double`
- `ActualValue` → `double`
- `IsSpike` → `bool`
