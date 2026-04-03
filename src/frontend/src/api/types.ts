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
  version: number;
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

export interface AcknowledgeResult {
  similarCount: number;
  similarPatterns: { id: string; template: string; similarity: number }[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// Auth types
export interface AuthStatusResponse {
  initialized: boolean;
}

export interface UserInfo {
  id: string;
  username: string;
  displayName: string;
  isAdmin: boolean;
  canInvite: boolean;
}

export interface AuthLoginResponse {
  token: string;
  user: UserInfo;
}

export interface CredentialInfo {
  id: string;
  deviceInfo: string | null;
  createdAt: string;
}

// Invite types
export interface InviteResponse {
  id: string;
  grantCanInvite: boolean;
  expiresAt: string;
  usedByUsername: string | null;
  usedAt: string | null;
  createdAt: string;
  inviteUrl: string | null;
}

// User management types
export interface UserResponse {
  id: string;
  username: string;
  displayName: string;
  isAdmin: boolean;
  canInvite: boolean;
  isDisabled: boolean;
  createdAt: string;
  invitedBy: string | null;
}
