export type AlertStatus = 'Firing' | 'FiringSuppressed' | 'Acknowledged' | 'Resolved';
export type ErrorSeverity = 'Info' | 'Warning' | 'Critical';
export type ErrorStatus = 'Active' | 'Resolved' | 'Ignored' | 'Expected';
export type ThresholdType = 'Absolute' | 'PercentageIncrease' | 'StandardDeviation';

export interface AlertDto {
  id: string;
  knownErrorId: string;
  knownErrorMessage: string | null;
  status: AlertStatus;
  thresholdType: ThresholdType;
  thresholdValue: number;
  actualValue: number;
  notificationCount: number;
  lastNotifiedAt: string | null;
  acknowledgedAt: string | null;
  resolvedAt: string | null;
  createdAt: string;
}

export interface AlertListResponse {
  items: AlertDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CorrelatedSpikeAlertDto {
  id: string;
  dataSourceId: string;
  dataSourceName: string | null;
  status: AlertStatus;
  alertIds: string;
  groupCount: number;
  detectedAt: string;
  resolvedAt: string | null;
  createdAt: string;
}

export interface ErrorGroupResponse {
  id: string;
  fingerprintHash: string;
  representativeMessage: string;
  severity: ErrorSeverity;
  status: ErrorStatus;
  firstSeen: string;
  lastSeen: string;
  totalOccurrences: number;
  dataSourceId: string | null;
  dataSourceName: string | null;
}

export interface ErrorGroupsPagedResponse {
  items: ErrorGroupResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ClassificationQueueResponse {
  id: string;
  knownErrorId: string;
  message: string;
  stackTrace: string | null;
  suggestedTags: TagSuggestionResponse[];
  confidence: number | null;
  severity: ErrorSeverity;
  status: ErrorStatus;
  firstSeen: string;
  lastSeen: string;
  totalOccurrences: number;
  createdAt: string;
}

export interface TagSuggestionResponse {
  tagId: string;
  tagName: string;
  confidence: number;
}

export interface ClassificationQueuePagedResponse {
  items: ClassificationQueueResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type AdapterType = 'Elasticsearch' | 'PostgreSql' | 'LogFile';

export interface FingerprintConfigResponse {
  id: string;
  dataSourceId: string;
  fieldName: string;
  order: number;
  normalizeBeforeHash: boolean;
  createdAt: string;
}

export interface CreateFingerprintConfigRequest {
  fieldName: string;
  order: number;
  normalizeBeforeHash?: boolean;
}

export interface DataSourceResponse {
  id: string;
  name: string;
  adapterType: AdapterType;
  connectionConfig: string;
  pollIntervalSeconds: number;
  schemaMapping: string | null;
  samplingBudget: number;
  enabled: boolean;
  createdAt: string;
  updatedAt: string;
  fingerprintConfigs: FingerprintConfigResponse[];
}

export interface CreateDataSourceRequest {
  name: string;
  adapterType: AdapterType;
  connectionConfig: string;
  pollIntervalSeconds?: number;
  schemaMapping?: string;
  samplingBudget?: number;
  enabled?: boolean;
}

export interface UpdateDataSourceRequest {
  name?: string;
  adapterType?: AdapterType;
  connectionConfig?: string;
  pollIntervalSeconds?: number;
  schemaMapping?: string;
  samplingBudget?: number;
  enabled?: boolean;
}

export interface ConnectionTestResponse {
  success: boolean;
  errorMessage: string | null;
  latencyMs: number;
  metadata: Record<string, unknown> | null;
}

export interface SchemaResponse {
  fields: FieldDefinitionDto[];
}

export interface FieldDefinitionDto {
  name: string;
  type: string;
  isNullable: boolean;
}

export interface SampleRecordsResponse {
  records: RawLogEntryDto[];
}

export interface RawLogEntryDto {
  timestamp: string;
  fields: Record<string, unknown>;
}

export interface ErrorGroupDetailResponse extends ErrorGroupResponse {
  representativeStackTrace: string | null;
}

export interface ErrorOccurrenceResponse {
  windowStart: string;
  windowEnd: string;
  count: number;
  sampleRatio: number;
  extrapolatedCount: number;
}

export type TagType = 'Manual' | 'Auto';

export interface TagResponse {
  id: string;
  name: string;
  tagType: TagType;
  color: string | null;
  createdAt: string;
}

export interface CreateTagRequest {
  name: string;
  tagType?: string;
  color?: string;
}

export interface UpdateTagRequest {
  name?: string;
  color?: string;
}

export interface SpikeDetectionRuleDto {
  id: string;
  knownErrorId: string | null;
  knownErrorMessage: string | null;
  thresholdType: ThresholdType;
  thresholdValue: number;
  windowMinutes: number;
  lookbackMinutes: number;
  enabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSpikeDetectionRuleRequest {
  knownErrorId?: string;
  thresholdType: ThresholdType;
  thresholdValue: number;
  windowMinutes?: number;
  lookbackMinutes?: number;
  enabled?: boolean;
}

export interface UpdateSpikeDetectionRuleRequest {
  thresholdType?: ThresholdType;
  thresholdValue?: number;
  windowMinutes?: number;
  lookbackMinutes?: number;
  enabled?: boolean;
}

export interface ConfigurationResponse {
  key: string;
  value: string;
  description: string | null;
  updatedAt: string;
}

export interface UpdateConfigurationRequest {
  key: string;
  value: string;
}

export interface DeletionImpactResponse {
  errorGroupCount: number;
  occurrenceCount: number;
  alertCount: number;
  classificationQueueCount: number;
  tagCount: number;
  ruleCount: number;
}

export interface DetectedFieldDto {
  name: string;
  type: string;
  proposedRole: string | null;
}

export interface DetectedConfigDto {
  filePath: string;
  parseMode: string;
  timestampField: string | null;
  levelField: string | null;
  messageField: string | null;
  regexPattern: string | null;
}

export interface DetectResponse {
  detectedFormat: string;
  fields: DetectedFieldDto[];
  sampleRecords: Record<string, unknown>[];
  proposedConfig: DetectedConfigDto;
}
