import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth, useAuthStatus } from './api/hooks/useAuth';
import Layout from './components/Layout';
import Login from './pages/Login';
import Setup from './pages/Setup';
import Register from './pages/Register';
import Dashboard from './pages/Dashboard';
import DataSources from './pages/DataSources';
import Patterns from './pages/Patterns';
import PatternDetail from './pages/PatternDetail';
import Settings from './pages/Settings';
import Users from './pages/Users';
import CircularProgress from '@mui/material/CircularProgress';
import Box from '@mui/material/Box';
import type { ReactNode } from 'react';

interface ProtectedRouteProps {
  children: ReactNode;
}

function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) {
    return <Navigate to="/" replace />;
  }
  return <>{children}</>;
}

function AdminRoute({ children }: ProtectedRouteProps) {
  const { isAuthenticated, user } = useAuth();
  if (!isAuthenticated) return <Navigate to="/" replace />;
  if (!user?.isAdmin) return <Navigate to="/dashboard" replace />;
  return <>{children}</>;
}

export default function App() {
  const { isAuthenticated } = useAuth();
  const { data: status, isLoading } = useAuthStatus();

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh' }}>
        <CircularProgress />
      </Box>
    );
  }

  const initialized = status?.initialized ?? true;

  return (
    <Routes>
      {/* Public routes */}
      <Route path="/register" element={<Register />} />
      <Route
        path="/setup"
        element={initialized ? <Navigate to="/" replace /> : <Setup />}
      />
      <Route
        path="/"
        element={
          !initialized ? (
            <Navigate to="/setup" replace />
          ) : isAuthenticated ? (
            <Navigate to="/dashboard" replace />
          ) : (
            <Login />
          )
        }
      />

      {/* Protected routes */}
      <Route
        element={
          <ProtectedRoute>
            <Layout />
          </ProtectedRoute>
        }
      >
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/patterns" element={<Patterns />} />
        <Route path="/data-sources" element={<DataSources />} />
        <Route path="/patterns/:id" element={<PatternDetail />} />
        <Route path="/settings" element={<Settings />} />
        <Route
          path="/users"
          element={
            <AdminRoute>
              <Users />
            </AdminRoute>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}
