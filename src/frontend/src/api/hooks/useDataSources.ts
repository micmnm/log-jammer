import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut, apiDelete } from '../client';
import type { DataSourceResponse, DataSourceType } from '../types';

const QUERY_KEY = ['datasources'];

export function useDataSources() {
  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: () => apiGet<DataSourceResponse[]>('/datasources'),
  });
}

interface CreateDataSourceRequest {
  name: string;
  type: DataSourceType;
  connectionConfig: string;
  messageTemplate?: string;
}

export function useCreateDataSource() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateDataSourceRequest) =>
      apiPost<DataSourceResponse>('/datasources', data),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}

interface UpdateDataSourceRequest {
  id: string;
  name?: string;
  connectionConfig?: string;
  messageTemplate?: string;
  enabled?: boolean;
}

export function useUpdateDataSource() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: UpdateDataSourceRequest) =>
      apiPut<DataSourceResponse>(`/datasources/${id}`, data),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}

export function useDeleteDataSource() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiDelete(`/datasources/${id}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}
