import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiDelete } from '../client';
import { startRegistration } from '@simplewebauthn/browser';
import type { PublicKeyCredentialCreationOptionsJSON } from '@simplewebauthn/browser';
import type { CredentialInfo } from '../types';

export function useMyCredentials() {
  return useQuery({
    queryKey: ['my-credentials'],
    queryFn: () => apiGet<CredentialInfo[]>('/users/me/credentials'),
  });
}

export function useAddPasskey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const options = await apiPost<PublicKeyCredentialCreationOptionsJSON>(
        '/auth/webauthn/register-options'
      );
      const attestation = await startRegistration({ optionsJSON: options });
      return apiPost<CredentialInfo>('/auth/webauthn/register', attestation);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['my-credentials'] });
    },
  });
}

export function useRemovePasskey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiDelete(`/users/me/credentials/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['my-credentials'] });
    },
  });
}
