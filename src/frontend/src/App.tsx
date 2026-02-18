import { useRef } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { QueryClient, QueryClientProvider, MutationCache } from '@tanstack/react-query';
import theme from './theme';
import { NotificationProvider, useNotification } from './contexts/NotificationContext';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ApiRequestError } from './api/client';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import ErrorGroups from './pages/ErrorGroups';
import ErrorGroupDetail from './pages/ErrorGroupDetail';
import Alerts from './pages/Alerts';
import Classification from './pages/Classification';
import DataSources from './pages/DataSources';
import Settings from './pages/Settings';
import Login from './pages/Login';
import NotFound from './pages/NotFound';

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function AppInner() {
  const { showNotification } = useNotification();

  // Use a ref so the QueryClient singleton always has the latest callback
  const showRef = useRef(showNotification);
  showRef.current = showNotification;

  const queryClient = useRef(
    new QueryClient({
      defaultOptions: {
        queries: {
          staleTime: 5000,
          retry: 1,
        },
      },
      mutationCache: new MutationCache({
        onError: (error) => {
          const message =
            error instanceof ApiRequestError
              ? error.message
              : 'An unexpected error occurred.';
          showRef.current(message);
        },
      }),
    }),
  ).current;

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route
            element={
              <ProtectedRoute>
                <Layout />
              </ProtectedRoute>
            }
          >
            <Route path="/" element={<Dashboard />} />
            <Route path="/error-groups" element={<ErrorGroups />} />
            <Route path="/error-groups/:id" element={<ErrorGroupDetail />} />
            <Route path="/alerts" element={<Alerts />} />
            <Route path="/classification" element={<Classification />} />
            <Route path="/data-sources" element={<DataSources />} />
            <Route path="/settings" element={<Settings />} />
            <Route path="*" element={<NotFound />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <NotificationProvider>
        <AuthProvider>
          <AppInner />
        </AuthProvider>
      </NotificationProvider>
    </ThemeProvider>
  );
}
