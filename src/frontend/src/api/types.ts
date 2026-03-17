export type DataSourceType = 'KibanaProxy' | 'Elasticsearch';
export type Severity = 'Info' | 'Warning' | 'Error' | 'Critical';

export interface DataSourceResponse {
  id: string;
  name: string;
  type: DataSourceType;
  connectionConfig: string;
  messageTemplate: string | null;
  enabled: boolean;
  createdAt: string;
  lastPolledAt: string | null;
}

export interface PatternListItem {
  id: string;
  template: string;
  severity: Severity;
  firstSeen: string;
  lastSeen: string;
  isNew: boolean;
  currentRate: number;
  expectedRate: number;
  stdDevsFromMean: number;
  dataSourceName: string;
}

export interface PatternDetailResponse extends PatternListItem {
  sampleMessage: string;
  occurrences: { windowStart: string; count: number }[];
  baselineBands: { hourOfWeek: number; avgCount: number; stdDevCount: number }[];
}

export interface DashboardResponse {
  totalPatterns: number;
  newPatternCount: number;
  ingestionRatePerHour: number;
  topAnomalies: AnomalyItem[];
  newPatterns: NewPatternItem[];
}

export interface AnomalyItem {
  patternId: string;
  template: string;
  severity: Severity;
  currentRate: number;
  expectedRate: number;
  stdDevsFromMean: number;
  dataSourceName: string;
}

export interface NewPatternItem {
  patternId: string;
  template: string;
  severity: Severity;
  firstSeen: string;
  dataSourceName: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
