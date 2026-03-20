import { createContext, useContext, useState, useCallback } from 'react';
import type { ReactNode } from 'react';
import { createElement } from 'react';
import { useMutation } from '@tanstack/react-query';
import { apiPost } from '../client';

interface AuthContextValue {
  token: string | null;
  isAuthenticated: boolean;
  setToken: (token: string) => void;
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

  const setToken = useCallback((newToken: string) => {
    localStorage.setItem('auth_token', newToken);
    setTokenState(newToken);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('auth_token');
    setTokenState(null);
  }, []);

  const value: AuthContextValue = {
    token,
    isAuthenticated: token !== null,
    setToken,
    logout,
  };

  return createElement(AuthContext.Provider, { value }, children);
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}

interface LoginResponse {
  token: string;
}

export function useLogin() {
  const { setToken } = useAuth();
  return useMutation({
    mutationFn: (password: string) =>
      apiPost<LoginResponse>('/auth/login', { password }),
    onSuccess: (data) => {
      setToken(data.token);
    },
  });
}
