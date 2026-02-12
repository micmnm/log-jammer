import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import type {
  SpikeDetectionRuleDto,
  CreateSpikeDetectionRuleRequest,
  UpdateSpikeDetectionRuleRequest,
} from '../types';

export function useSpikeDetectionRules() {
  return useQuery({
    queryKey: ['spikedetectionrules'],
    queryFn: () => api.get<SpikeDetectionRuleDto[]>('/spikedetectionrules'),
  });
}

export function useCreateSpikeDetectionRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSpikeDetectionRuleRequest) =>
      api.post<SpikeDetectionRuleDto>('/spikedetectionrules', request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['spikedetectionrules'] });
    },
  });
}

export function useUpdateSpikeDetectionRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateSpikeDetectionRuleRequest }) =>
      api.put<SpikeDetectionRuleDto>(`/spikedetectionrules/${id}`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['spikedetectionrules'] });
    },
  });
}

export function useDeleteSpikeDetectionRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/spikedetectionrules/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['spikedetectionrules'] });
    },
  });
}
