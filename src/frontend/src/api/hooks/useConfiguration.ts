import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import type { ConfigurationResponse, UpdateConfigurationRequest } from '../types';

export function useConfiguration() {
  return useQuery({
    queryKey: ['configuration'],
    queryFn: () => api.get<ConfigurationResponse[]>('/configuration'),
  });
}

export function useUpdateConfiguration() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateConfigurationRequest) =>
      api.put<ConfigurationResponse>('/configuration', request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configuration'] });
    },
  });
}
