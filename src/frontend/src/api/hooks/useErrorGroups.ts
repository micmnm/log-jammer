import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import type {
  ErrorGroupsPagedResponse,
  ErrorGroupDetailResponse,
  ErrorOccurrenceResponse,
  ErrorSeverity,
  ErrorStatus,
} from '../types';

interface UseErrorGroupsParams {
  dataSourceId?: string;
  status?: ErrorStatus;
  severity?: ErrorSeverity;
  page?: number;
  pageSize?: number;
}

export function useErrorGroups({
  dataSourceId,
  status,
  severity,
  page = 1,
  pageSize = 25,
}: UseErrorGroupsParams = {}) {
  const params = new URLSearchParams();
  if (dataSourceId) params.set('dataSourceId', dataSourceId);
  if (status) params.set('status', status);
  if (severity) params.set('severity', severity);
  params.set('page', String(page));
  params.set('pageSize', String(pageSize));

  return useQuery({
    queryKey: ['errorgroups', dataSourceId, status, severity, page, pageSize],
    queryFn: () => api.get<ErrorGroupsPagedResponse>(`/errorgroups?${params}`),
  });
}

export function useErrorGroup(id: string) {
  return useQuery({
    queryKey: ['errorgroups', id],
    queryFn: () => api.get<ErrorGroupDetailResponse>(`/errorgroups/${id}`),
    enabled: !!id,
  });
}

export function useErrorGroupOccurrences(id: string, from?: string, to?: string) {
  const params = new URLSearchParams();
  if (from) params.set('from', from);
  if (to) params.set('to', to);
  const qs = params.toString();

  return useQuery({
    queryKey: ['errorgroups', id, 'occurrences', from, to],
    queryFn: () =>
      api.get<ErrorOccurrenceResponse[]>(`/errorgroups/${id}/occurrences${qs ? `?${qs}` : ''}`),
    enabled: !!id,
  });
}

export function useUpdateErrorGroupStatus() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: ErrorStatus }) =>
      api.put(`/errorgroups/${id}/status`, { status }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['errorgroups'] });
    },
  });
}

export function useUpdateErrorGroupSeverity() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, severity }: { id: string; severity: ErrorSeverity }) =>
      api.put(`/errorgroups/${id}/severity`, { severity }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['errorgroups'] });
    },
  });
}
