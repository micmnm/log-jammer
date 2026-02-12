import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import type { ClassificationQueuePagedResponse } from '../types';

export function useClassificationQueue(page = 1, pageSize = 10) {
  const params = new URLSearchParams();
  params.set('page', String(page));
  params.set('pageSize', String(pageSize));

  return useQuery({
    queryKey: ['classification', 'queue', page, pageSize],
    queryFn: () => api.get<ClassificationQueuePagedResponse>(`/classification/queue?${params}`),
  });
}

export function useApproveClassification() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, tagIds }: { id: string; tagIds: string[] }) =>
      api.post(`/classification/queue/${id}/approve`, { tagIds }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['classification'] });
    },
  });
}

export function useRejectClassification() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      id,
      correctTagIds,
      reason,
    }: {
      id: string;
      correctTagIds: string[];
      reason?: string;
    }) => api.post(`/classification/queue/${id}/reject`, { correctTagIds, reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['classification'] });
    },
  });
}
