import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import type { FingerprintConfigResponse, CreateFingerprintConfigRequest } from '../types';

export function useFingerprintConfigs(dataSourceId: string) {
  return useQuery({
    queryKey: ['datasources', dataSourceId, 'fingerprint-configs'],
    queryFn: () =>
      api.get<FingerprintConfigResponse[]>(`/datasources/${dataSourceId}/fingerprint-configs`),
    enabled: !!dataSourceId,
  });
}

export function useCreateFingerprintConfig(dataSourceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateFingerprintConfigRequest) =>
      api.post<FingerprintConfigResponse>(`/datasources/${dataSourceId}/fingerprint-configs`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['datasources', dataSourceId, 'fingerprint-configs'] });
      queryClient.invalidateQueries({ queryKey: ['datasources', dataSourceId] });
    },
  });
}

export function useDeleteFingerprintConfig(dataSourceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/datasources/${dataSourceId}/fingerprint-configs/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['datasources', dataSourceId, 'fingerprint-configs'] });
      queryClient.invalidateQueries({ queryKey: ['datasources', dataSourceId] });
    },
  });
}
