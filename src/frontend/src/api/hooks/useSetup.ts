import { useMutation } from '@tanstack/react-query';
import { apiPost } from '../client';
import { startRegistration } from '@simplewebauthn/browser';
import type { PublicKeyCredentialCreationOptionsJSON } from '@simplewebauthn/browser';
import { useAuth } from './useAuth';
import type { AuthLoginResponse } from '../types';

interface SetupParams {
  token: string;
  username: string;
  displayName: string;
}

export function useSetupAdmin() {
  const { setAuth } = useAuth();
  return useMutation({
    mutationFn: async ({ token, username, displayName }: SetupParams) => {
      const options = await apiPost<PublicKeyCredentialCreationOptionsJSON>(
        '/auth/setup/options',
        { token, username, displayName }
      );
      const attestation = await startRegistration({ optionsJSON: options });
      return apiPost<AuthLoginResponse>('/auth/setup/register', attestation);
    },
    onSuccess: (data) => {
      setAuth(data.token, data.user);
    },
  });
}
