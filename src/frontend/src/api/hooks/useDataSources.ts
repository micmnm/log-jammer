import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import type {
  DataSourceResponse,
  CreateDataSourceRequest,
  UpdateDataSourceRequest,
  ConnectionTestResponse,
  SchemaResponse,
  SampleRecordsResponse,
  DetectResponse,
} from '../types';

export function useDataSources() {
  return useQuery({
    queryKey: ['datasources'],
    queryFn: () => api.get<DataSourceResponse[]>('/datasources'),
  });
}

export function useDataSource(id: string) {
  return useQuery({
    queryKey: ['datasources', id],
    queryFn: () => api.get<DataSourceResponse>(`/datasources/${id}`),
    enabled: !!id,
  });
}

export function useCreateDataSource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateDataSourceRequest) =>
      api.post<DataSourceResponse>('/datasources', request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['datasources'] });
    },
  });
}

export function useUpdateDataSource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateDataSourceRequest }) =>
      api.put<DataSourceResponse>(`/datasources/${id}`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['datasources'] });
    },
  });
}

export function useDeleteDataSource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/datasources/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['datasources'] });
    },
  });
}

export function useTestConnection() {
  return useMutation({
    mutationFn: (id: string) =>
      api.post<ConnectionTestResponse>(`/datasources/${id}/test-connection`),
  });
}

export function useDataSourceSchema(id: string) {
  return useQuery({
    queryKey: ['datasources', id, 'schema'],
    queryFn: () => api.get<SchemaResponse>(`/datasources/${id}/schema`),
    enabled: !!id,
  });
}

export function useSampleRecords(id: string, count = 3) {
  return useQuery({
    queryKey: ['datasources', id, 'sample-records', count],
    queryFn: () => api.get<SampleRecordsResponse>(`/datasources/${id}/sample-records?count=${count}`),
    enabled: !!id,
  });
}

export function useDetectLogFile() {
  return useMutation({
    mutationFn: (filePath: string) =>
      api.post<DetectResponse>('/datasources/detect', { filePath }),
  });
}
