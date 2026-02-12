import { useQuery } from '@tanstack/react-query';
import { api } from '../client';
import type { AlertListResponse, ErrorGroupsPagedResponse, ClassificationQueuePagedResponse } from '../types';

export function useDashboardStats() {
  const firingAlerts = useQuery({
    queryKey: ['alerts', 'Firing', 1, 1],
    queryFn: () => api.get<AlertListResponse>('/alerts?status=Firing&page=1&pageSize=1'),
    refetchInterval: 5000,
  });

  const errorGroups = useQuery({
    queryKey: ['errorgroups', 'summary'],
    queryFn: () => api.get<ErrorGroupsPagedResponse>('/errorgroups?page=1&pageSize=1'),
    refetchInterval: 30000,
  });

  const unclassified = useQuery({
    queryKey: ['classification', 'queue', 'summary'],
    queryFn: () => api.get<ClassificationQueuePagedResponse>('/classification/queue?page=1&pageSize=1'),
    refetchInterval: 30000,
  });

  return {
    firingCount: firingAlerts.data?.totalCount ?? 0,
    errorGroupCount: errorGroups.data?.totalCount ?? 0,
    unclassifiedCount: unclassified.data?.totalCount ?? 0,
    isLoading: firingAlerts.isLoading || errorGroups.isLoading || unclassified.isLoading,
  };
}
