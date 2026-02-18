# Definition DTO

## Authentication

### AuthSettings
`LogJammer.Api.Auth.AuthSettings`
- `Username` (string, default: "admin")
- `Password` (string, default: "changeme")
- `ApiToken` (string, default: random GUID)

### LoginRequest
`LogJammer.Api.Auth.LoginRequest` (record)
- `Username` (string)
- `Password` (string)

### LoginResponse
`LogJammer.Api.Auth.LoginResponse` (record)
- `Token` (string)

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
| DataSourceId | Guid? | data_source_id | FK → data_sources (nullable, null = orphaned/preserved) |
| CreatedAt | DateTime | created_at | auto-set |
| UpdatedAt | DateTime | updated_at | auto-set |

Navigation properties: `DataSource`, `ErrorTags`, `Occurrences`, `UserOverrides`, `Alerts`, `FingerprintAliases`

### FingerprintAlias
`LogJammer.Core.Entities.FingerprintAlias` → `fingerprint_aliases`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK, auto-generated |
| FingerprintHash | string | fingerprint_hash | max 64, unique index |
| KnownErrorId | Guid | known_error_id | FK → known_errors (cascade delete) |
| CreatedAt | DateTime | created_at | auto-set |

Maps merged fingerprint hashes to their target KnownError. When ClassificationProcessor detects two KnownErrors are semantically identical (via embedding similarity), it merges them and creates an alias so future ingestion routes the variant hash directly to the surviving group.

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
| ConsecutiveBelowThreshold | int | consecutive_below_threshold | auto-resolve tracking |
| CreatedAt | DateTime | created_at | auto-set |
| UpdatedAt | DateTime | updated_at | auto-set |

### SpikeDetectionRule
`LogJammer.Core.Entities.SpikeDetectionRule` → `spike_detection_rules`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK |
| KnownErrorId | Guid? | known_error_id | FK → known_errors, unique, null = global default |
| ThresholdType | ThresholdType | threshold_type | stored as string |
| ThresholdValue | double | threshold_value | |
| WindowMinutes | int | window_minutes | evaluation window (default 5) |
| LookbackMinutes | int | lookback_minutes | baseline lookback (default 1440 = 24h) |
| Enabled | bool | enabled | default true |
| CreatedAt | DateTime | created_at | auto-set |
| UpdatedAt | DateTime | updated_at | auto-set |

### CorrelatedSpikeAlert
`LogJammer.Core.Entities.CorrelatedSpikeAlert` → `correlated_spike_alerts`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK |
| DataSourceId | Guid | data_source_id | FK → data_sources |
| Status | AlertStatus | status | stored as string |
| AlertIds | string | alert_ids | JSON array of related Alert Ids |
| GroupCount | int | group_count | number of groups that spiked |
| DetectedAt | DateTime | detected_at | |
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

### ClassificationConfig
`LogJammer.Core.Entities.ClassificationConfig` → `classification_config`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK, auto-generated |
| Key | string | key | max 100, unique index |
| Value | string | value | max 500 |
| Description | string? | description | max 500 |
| CreatedAt | DateTime | created_at | auto-set |
| UpdatedAt | DateTime | updated_at | auto-set |

### TagCentroid
`LogJammer.Core.Entities.TagCentroid` → `tag_centroids`

| Property | Type | DB Column | Notes |
|----------|------|-----------|-------|
| Id | Guid | id | PK, auto-generated |
| TagId | Guid | tag_id | FK → tags, unique index |
| CentroidVector | Vector? | centroid_vector | vector(384) |
| ErrorCount | int | error_count | |
| UpdatedAt | DateTime | updated_at | auto-set |

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
- `GetByFingerprintAliasAsync(fingerprintHash)` → `Task<KnownError?>` — looks up via `fingerprint_aliases` table
- `MergeIntoAsync(sourceKnownErrorId, targetKnownErrorId)` → `Task` — moves occurrences, creates alias, deletes source (idempotent)

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

### IAlertRepository
`LogJammer.Core.Interfaces.IAlertRepository`
- `GetActiveByKnownErrorIdAsync(knownErrorId)` → `Task<Alert?>`
- `GetAllAsync(status?, dataSourceId?, page, pageSize)` → `Task<IReadOnlyList<Alert>>`
- `GetCountAsync(status?, dataSourceId?)` → `Task<int>`
- `GetByIdAsync(id)` → `Task<Alert?>`
- `AddAsync(alert)` → `Task<Alert>`
- `UpdateAsync(alert)` → `Task`
- `GetRecentByDataSourceAsync(dataSourceId, since)` → `Task<IReadOnlyList<Alert>>`

### ISpikeDetectionRuleRepository
`LogJammer.Core.Interfaces.ISpikeDetectionRuleRepository`
- `GetByKnownErrorIdAsync(knownErrorId?)` → `Task<SpikeDetectionRule?>`
- `GetGlobalDefaultAsync()` → `Task<SpikeDetectionRule?>`
- `GetByIdAsync(id)` → `Task<SpikeDetectionRule?>`
- `GetAllAsync()` → `Task<IReadOnlyList<SpikeDetectionRule>>`
- `AddAsync(rule)` → `Task<SpikeDetectionRule>`
- `UpdateAsync(rule)` → `Task`
- `DeleteAsync(id)` → `Task`

### ICorrelatedSpikeAlertRepository
`LogJammer.Core.Interfaces.ICorrelatedSpikeAlertRepository`
- `GetAllAsync(status?, page, pageSize)` → `Task<IReadOnlyList<CorrelatedSpikeAlert>>`
- `GetActiveByDataSourceIdAsync(dataSourceId)` → `Task<CorrelatedSpikeAlert?>`
- `AddAsync(alert)` → `Task<CorrelatedSpikeAlert>`
- `UpdateAsync(alert)` → `Task`

### IAlertManager
`LogJammer.Core.Interfaces.IAlertManager`
- `ProcessSpikeResultAsync(result, dataSourceId)` → `Task`
- `AcknowledgeAsync(alertId)` → `Task`
- `ResolveAsync(alertId)` → `Task`

### ICorrelationDetector
`LogJammer.Core.Interfaces.ICorrelationDetector`
- `DetectAsync(dataSourceId)` → `Task`

### IClassificationService
`LogJammer.Core.Interfaces.IClassificationService`
- `ClassifyAsync(error)` → `Task<ClassificationResult>`
- `RecalculateTagCentroidAsync(tagId)` → `Task`
- `RecalculateAllCentroidsAsync()` → `Task`

### IClassificationConfigRepository
`LogJammer.Core.Interfaces.IClassificationConfigRepository`
- `GetAsync(key)` → `Task<ClassificationConfig?>`
- `GetAllAsync()` → `Task<IReadOnlyList<ClassificationConfig>>`
- `UpsertAsync(key, value, description?)` → `Task<ClassificationConfig>`

### IClassificationQueueRepository
`LogJammer.Core.Interfaces.IClassificationQueueRepository`
- `GetPendingAsync(page, pageSize)` → `Task<IReadOnlyList<ClassificationQueueItem>>`
- `GetPendingCountAsync()` → `Task<int>`
- `GetByIdAsync(id)` → `Task<ClassificationQueueItem?>`
- `UpdateAsync(item)` → `Task`
- `GetUnprocessedAsync(batchSize)` → `Task<IReadOnlyList<ClassificationQueueItem>>`

### IUserOverrideRepository
`LogJammer.Core.Interfaces.IUserOverrideRepository`
- `AddAsync(override)` → `Task<UserOverride>`
- `GetByKnownErrorAsync(knownErrorId)` → `Task<IReadOnlyList<UserOverride>>`
- `GetByKnownErrorAndTypeAsync(knownErrorId, type)` → `Task<UserOverride?>`

### IFingerprintConfigRepository
`LogJammer.Core.Interfaces.IFingerprintConfigRepository`
- `GetByDataSourceIdAsync(dataSourceId)` → `Task<IReadOnlyList<FingerprintConfig>>`
- `GetByIdAsync(id)` → `Task<FingerprintConfig?>`
- `AddAsync(config)` → `Task<FingerprintConfig>`
- `UpdateAsync(config)` → `Task`
- `DeleteAsync(config)` → `Task`

### ITagRepository
`LogJammer.Core.Interfaces.ITagRepository`
- `GetAllAsync()` → `Task<IReadOnlyList<Tag>>`
- `GetByIdAsync(id)` → `Task<Tag?>`
- `GetByNameAsync(name)` → `Task<Tag?>`
- `AddAsync(tag)` → `Task<Tag>`
- `UpdateAsync(tag)` → `Task`
- `DeleteAsync(tag)` → `Task`

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

### ClassificationResult
`LogJammer.Core.Models.ClassificationResult`
- `MatchedErrorGroupId` → `Guid?`
- `SimilarityScore` → `double`
- `SuggestedTags` → `IReadOnlyList<TagSuggestion>`
- `NeedsReview` → `bool`

### TagSuggestion
`LogJammer.Core.Models.TagSuggestion`
- `TagId` → `Guid`
- `TagName` → `string`
- `Confidence` → `double`

### DeletionImpact
`LogJammer.Core.Models.DeletionImpact`
- `ErrorGroupCount` → `int`
- `OccurrenceCount` → `int`
- `AlertCount` → `int`
- `ClassificationQueueCount` → `int`
- `TagCount` → `int`
- `RuleCount` → `int`

### LogFileConnectionConfig
`LogJammer.Infrastructure.Adapters.LogFile.LogFileConnectionConfig`
- `FilePath` → `string` (required, singular file path)
- `ParseMode` → `string` (default: "jsonlines"; options: "jsonlines", "regex")
- `RegexPattern` → `string?`
- `TimestampField` → `string?`
- `TimestampFormat` → `string?`
- `LevelField` → `string?`
- `MessageField` → `string?`

### DetectResult
`LogJammer.Core.Interfaces.DetectResult`
- `DetectedFormat` → `string` ("jsonlines" or "text")
- `Fields` → `IReadOnlyList<DetectedField>`
- `SampleRecords` → `IReadOnlyList<Dictionary<string, object?>>`
- `ProposedConfig` → `DetectedConfig`

### DetectedField
`LogJammer.Core.Interfaces.DetectedField`
- `Name` → `string`
- `Type` → `string`
- `ProposedRole` → `string?` ("Timestamp", "Level", "Message", or null)

### DetectedConfig
`LogJammer.Core.Interfaces.DetectedConfig`
- `FilePath` → `string`
- `ParseMode` → `string`
- `TimestampField` → `string?`
- `LevelField` → `string?`
- `MessageField` → `string?`
- `RegexPattern` → `string?`

### DetectRequest / DetectResponse (DTOs)
`LogJammer.Api.Dtos.DetectDtos`
- `DetectRequest`: `FilePath` (required)
- `DetectResponse`: `DetectedFormat`, `Fields` → `DetectedFieldDto[]`, `SampleRecords`, `ProposedConfig` → `DetectedConfigDto`
- `DetectedFieldDto`: `Name`, `Type`, `ProposedRole`
- `DetectedConfigDto`: `FilePath`, `ParseMode`, `TimestampField`, `LevelField`, `MessageField`, `RegexPattern`

### ClassificationQueueResponse / ClassificationQueuePagedResponse (DTOs)
`LogJammer.Api.Dtos.ClassificationDtos`
- `ClassificationQueueResponse`: `Id`, `KnownErrorId`, `Message`, `StackTrace`, `SuggestedTags` → `List<TagSuggestionResponse>`, `Confidence`, `Severity` (ErrorSeverity), `Status` (ErrorStatus), `FirstSeen`, `LastSeen`, `TotalOccurrences`, `CreatedAt`
- `TagSuggestionResponse`: `TagId`, `TagName`, `Confidence`
- `ClassificationQueuePagedResponse`: `Items` → `IReadOnlyList<ClassificationQueueResponse>`, `TotalCount`, `Page`, `PageSize`
- `ApproveClassificationRequest`: `TagIds` → `List<Guid>`
- `RejectClassificationRequest`: `CorrectTagIds` → `List<Guid>`, `Reason` (optional)

### DeletionImpactResponse (DTO)
`LogJammer.Api.Dtos.DataSourceDtos`
- `DeletionImpactResponse`: `ErrorGroupCount`, `OccurrenceCount`, `AlertCount`, `ClassificationQueueCount`, `TagCount`, `RuleCount` (all `int`)

---

## Frontend (TypeScript)

Location: `src/frontend/src/api/types.ts`

TypeScript type definitions mirror the backend DTOs. Generated from backend API responses.

### Key Types
- `AlertDto`, `AlertListResponse` – mirrors `LogJammer.Api.Dtos.AlertDto`/`AlertListResponse`
- `CorrelatedSpikeAlertDto` – mirrors `LogJammer.Api.Dtos.CorrelatedSpikeAlertDto`
- `ErrorGroupResponse`, `ErrorGroupsPagedResponse` – mirrors `LogJammer.Api.Dtos.ErrorGroupResponse` (dataSourceId is `string | null` for orphaned groups)
- `ErrorGroupDetailResponse` – extends `ErrorGroupResponse` with `representativeStackTrace`
- `ErrorOccurrenceResponse` – mirrors occurrence window data (windowStart, windowEnd, count, sampleRatio, extrapolatedCount)
- `DataSourceResponse` – mirrors `LogJammer.Api.Dtos.DataSourceResponse` (id, name, adapterType, connectionConfig, pollIntervalSeconds, schemaMapping, samplingBudget, enabled, fingerprintConfigs)
- `CreateDataSourceRequest`, `UpdateDataSourceRequest` – data source CRUD request types
- `ConnectionTestResponse` – connection test result (success, errorMessage, latencyMs, metadata)
- `SchemaResponse`, `FieldDefinitionDto` – schema discovery types
- `SampleRecordsResponse`, `RawLogEntryDto` – sample records types
- `DeletionImpactResponse` – deletion impact counts (errorGroupCount, occurrenceCount, alertCount, classificationQueueCount, tagCount, ruleCount)
- `FingerprintConfigResponse`, `CreateFingerprintConfigRequest` – fingerprint config types
- `TagResponse` – mirrors `LogJammer.Api.Dtos.TagResponse` (id, name, tagType, color, createdAt)
- `CreateTagRequest`, `UpdateTagRequest` – tag CRUD request types
- `SpikeDetectionRuleDto`, `CreateSpikeDetectionRuleRequest`, `UpdateSpikeDetectionRuleRequest` – spike detection rule types
- `ConfigurationResponse`, `UpdateConfigurationRequest` – classification configuration types
- `ClassificationQueueResponse`, `ClassificationQueuePagedResponse` – mirrors `LogJammer.Api.Dtos.ClassificationQueueResponse` (includes severity, status, firstSeen, lastSeen, totalOccurrences from KnownError)
- Enums as string union types: `AlertStatus`, `ErrorSeverity`, `ErrorStatus`, `ThresholdType`, `AdapterType`, `TagType`

### Hooks (`src/api/hooks/`)
- `useAlerts.ts` – useAlerts, useAlertHistory, useCorrelatedAlerts, useAcknowledgeAlert
- `useDashboard.ts` – useDashboardStats (aggregates alert/error/queue counts)
- `useDataSources.ts` – useDataSources, useDataSource, useCreateDataSource, useUpdateDataSource, useDeleteDataSource, useTestConnection, useDataSourceSchema, useSampleRecords, useDeletionImpact
- `useErrorGroups.ts` – useErrorGroups, useErrorGroup, useErrorGroupOccurrences, useUpdateErrorGroupStatus, useUpdateErrorGroupSeverity
- `useTags.ts` – useTags, useCreateTag, useUpdateTag, useDeleteTag
- `useClassification.ts` – useClassificationQueue, useApproveClassification, useRejectClassification
- `useFingerprintConfigs.ts` – useFingerprintConfigs, useCreateFingerprintConfig, useDeleteFingerprintConfig
- `useSpikeDetectionRules.ts` – useSpikeDetectionRules, useCreateSpikeDetectionRule, useUpdateSpikeDetectionRule, useDeleteSpikeDetectionRule
- `useConfiguration.ts` – useConfiguration, useUpdateConfiguration

### Pages
- `Dashboard` – stat cards, backpressure indicator, alerts feed
- `Alerts` – active/history tabs with pagination
- `ErrorGroups` – DataGrid with server-side pagination, severity/status/data source filters
- `ErrorGroupDetail` – header, metadata, severity/status controls, occurrence chart (Chart.js), stack trace accordion, related alerts
- `Classification` – paginated queue cards with ML suggestion box / UNMATCHED state, Accept Tags / Reject & Retag / Assign Tags flows, UNMATCHED filter chip, inline tag creation with color picker
- `DataSources` – table with CRUD, toggle enabled, test connection, schema mapping, fingerprint config dialogs
- `Settings` – tabs: Rules (spike detection CRUD), Tags (CRUD with color picker), Classification (key-value config editor)

### Shared Components
- `SeverityChip` – ErrorSeverity → MUI Chip color mapping
- `StatusChip` – ErrorStatus → MUI Chip variant/color mapping
- `ConfidenceBar` – LinearProgress with percentage label
- `BackpressureIndicator` – warning banner when data source samplingBudget < 0.5
- `ClassificationQueueCard` – queue item card with approve/reject actions and reject dialog
- `AlertsFeed`, `AlertCard` – alert display with acknowledge action
- `DataSourceDialog` – create/edit data source with adapter-specific connection fields
- `SchemaMappingDialog` – dropdown mapping of target fields to source fields with sample preview
- `FingerprintConfigDialog` – checkbox list with ordering and normalize toggle
- `settings/RulesTab`, `settings/RuleDialog` – spike detection rule management
- `settings/TagsTab`, `settings/TagDialog` – tag management with color picker
- `settings/ClassificationTab` – classification config key-value editor
