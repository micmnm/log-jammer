import { createContext, useContext, useState, useCallback } from 'react';
import type { ReactNode } from 'react';
import { createElement } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { apiGet, apiPost } from '../client';
import { startAuthentication } from '@simplewebauthn/browser';
import type { PublicKeyCredentialRequestOptionsJSON } from '@simplewebauthn/browser';
import type { AuthStatusResponse, AuthLoginResponse, UserInfo } from '../types';

interface AuthContextValue {
  token: string | null;
  user: UserInfo | null;
  isAuthenticated: boolean;
  setAuth: (token: string, user: UserInfo) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [token, setTokenState] = useState<string | null>(() =>
    localStorage.getItem('auth_token')
  );
  const [user, setUser] = useState<UserInfo | null>(() => {
    const stored = localStorage.getItem('auth_user');
    return stored ? JSON.parse(stored) : null;
  });

  const setAuth = useCallback((newToken: string, newUser: UserInfo) => {
    localStorage.setItem('auth_token', newToken);
    localStorage.setItem('auth_user', JSON.stringify(newUser));
    setTokenState(newToken);
    setUser(newUser);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_user');
    setTokenState(null);
    setUser(null);
  }, []);

  const value: AuthContextValue = {
    token,
    user,
    isAuthenticated: token !== null,
    setAuth,
    logout,
  };

  return createElement(AuthContext.Provider, { value }, children);
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}

export function useAuthStatus() {
  return useQuery({
    queryKey: ['auth-status'],
    queryFn: () => apiGet<AuthStatusResponse>('/auth/status'),
    staleTime: 30_000,
  });
}

export function usePasskeyLogin() {
  const { setAuth } = useAuth();
  return useMutation({
    mutationFn: async () => {
      const options = await apiPost<PublicKeyCredentialRequestOptionsJSON>(
        '/auth/webauthn/login-options'
      );
      const assertion = await startAuthentication({ optionsJSON: options });
      return apiPost<AuthLoginResponse>('/auth/webauthn/login', assertion);
    },
    onSuccess: (data) => {
      setAuth(data.token, data.user);
    },
  });
}
