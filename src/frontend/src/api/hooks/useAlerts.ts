import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import type { AlertListResponse, CorrelatedSpikeAlertDto } from '../types';

export function useAlerts(status?: string, page = 1, pageSize = 50) {
  const params = new URLSearchParams();
  if (status) params.set('status', status);
  params.set('page', String(page));
  params.set('pageSize', String(pageSize));

  return useQuery({
    queryKey: ['alerts', status, page, pageSize],
    queryFn: () => api.get<AlertListResponse>(`/alerts?${params}`),
    refetchInterval: 5000,
  });
}

export function useAlertHistory(page = 1, pageSize = 20) {
  const params = new URLSearchParams();
  params.set('page', String(page));
  params.set('pageSize', String(pageSize));

  return useQuery({
    queryKey: ['alerts', 'history', page, pageSize],
    queryFn: () => api.get<AlertListResponse>(`/alerts/history?${params}`),
  });
}

export function useCorrelatedAlerts(status?: string) {
  const params = new URLSearchParams();
  if (status) params.set('status', status);

  return useQuery({
    queryKey: ['alerts', 'correlated', status],
    queryFn: () => api.get<CorrelatedSpikeAlertDto[]>(`/alerts/correlated?${params}`),
    refetchInterval: 5000,
  });
}

export function useAcknowledgeAlert() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (alertId: string) => api.post(`/alerts/${alertId}/acknowledge`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['alerts'] });
    },
  });
}
