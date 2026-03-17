import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../client';
import type { PatternListItem, PatternDetailResponse, PagedResult, Severity } from '../types';

interface PatternFilters {
  dataSourceId?: string;
  severity?: Severity;
  isNew?: boolean;
  page?: number;
  pageSize?: number;
}

function buildQueryString(filters: PatternFilters): string {
  const params = new URLSearchParams();
  if (filters.dataSourceId) params.set('dataSourceId', filters.dataSourceId);
  if (filters.severity) params.set('severity', filters.severity);
  if (filters.isNew !== undefined) params.set('isNew', String(filters.isNew));
  if (filters.page !== undefined) params.set('page', String(filters.page));
  if (filters.pageSize !== undefined) params.set('pageSize', String(filters.pageSize));
  const qs = params.toString();
  return qs ? `?${qs}` : '';
}

export function usePatterns(filters: PatternFilters = {}) {
  return useQuery({
    queryKey: ['patterns', filters],
    queryFn: () =>
      apiGet<PagedResult<PatternListItem>>(`/patterns${buildQueryString(filters)}`),
  });
}

export function usePatternDetail(id: string) {
  return useQuery({
    queryKey: ['patterns', id],
    queryFn: () => apiGet<PatternDetailResponse>(`/patterns/${id}`),
    enabled: !!id,
  });
}

export function useAcknowledgePattern() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiPost<void>(`/patterns/${id}/acknowledge`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['patterns'] });
      void qc.invalidateQueries({ queryKey: ['dashboard'] });
    },
  });
}

interface AcknowledgeAllResult {
  acknowledged: number;
}

export function useAcknowledgeAll() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dataSourceId?: string) => {
      const qs = dataSourceId ? `?dataSourceId=${dataSourceId}` : '';
      return apiPost<AcknowledgeAllResult>(`/patterns/acknowledge-all${qs}`);
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['patterns'] });
      void qc.invalidateQueries({ queryKey: ['dashboard'] });
    },
  });
}
