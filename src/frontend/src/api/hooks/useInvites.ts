import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../client';
import { startRegistration } from '@simplewebauthn/browser';
import type { PublicKeyCredentialCreationOptionsJSON } from '@simplewebauthn/browser';
import { useAuth } from './useAuth';
import type { InviteResponse, AuthLoginResponse } from '../types';

export function useInvites() {
  return useQuery({
    queryKey: ['invites'],
    queryFn: () => apiGet<InviteResponse[]>('/invites'),
  });
}

export function useCreateInvite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (grantCanInvite: boolean) =>
      apiPost<InviteResponse>('/invites', { grantCanInvite }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invites'] });
    },
  });
}

interface InviteRegisterParams {
  token: string;
  username: string;
  displayName: string;
}

export function useInviteRegister() {
  const { setAuth } = useAuth();
  return useMutation({
    mutationFn: async ({ token, username, displayName }: InviteRegisterParams) => {
      const options = await apiPost<PublicKeyCredentialCreationOptionsJSON>(
        `/invites/${token}/register`,
        { token, username, displayName }
      );
      const attestation = await startRegistration({ optionsJSON: options });
      return apiPost<AuthLoginResponse>(`/invites/${token}/complete`, attestation);
    },
    onSuccess: (data) => {
      setAuth(data.token, data.user);
    },
  });
}
