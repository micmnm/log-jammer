import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../client';
import type { DashboardResponse } from '../types';

export function useDashboard() {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: () => apiGet<DashboardResponse>('/dashboard'),
    refetchInterval: 30_000,
  });
}
