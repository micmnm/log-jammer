import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../client';
import type { DashboardResponse } from '../types';
import { useAutoRefresh } from '../../AutoRefreshContext';

export function useDashboard() {
  const { refreshInterval } = useAutoRefresh();
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: () => apiGet<DashboardResponse>('/dashboard'),
    refetchInterval: refreshInterval || false,
  });
}
