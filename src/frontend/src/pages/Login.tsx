import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import {
  Box,
  Button,
  Card,
  CardContent,
  TextField,
  Typography,
  Alert,
  useTheme,
} from '@mui/material';
import { useAuth } from '../contexts/AuthContext';

export default function Login() {
  const theme = useTheme();
  const navigate = useNavigate();
  const { isAuthenticated, login } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  if (isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login(username, password);
      navigate('/', { replace: true });
    } catch {
      setError('Invalid username or password');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        bgcolor: 'background.default',
      }}
    >
      <Card sx={{ width: 400, maxWidth: '90vw' }}>
        <CardContent sx={{ p: 4 }}>
          <Typography
            variant="h5"
            sx={{
              mb: 1,
              fontFamily: theme.fontFamilyMono,
              color: 'primary.main',
              textAlign: 'center',
              letterSpacing: '0.1em',
            }}
          >
            LOG JAMMER
          </Typography>
          <Typography
            variant="body2"
            sx={{
              mb: 3,
              color: 'text.secondary',
              textAlign: 'center',
              fontFamily: theme.fontFamilyMono,
              fontSize: '0.7rem',
              letterSpacing: '0.05em',
            }}
          >
            AUTHENTICATION REQUIRED
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit}>
            <TextField
              label="Username"
              fullWidth
              autoFocus
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              sx={{ mb: 2 }}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Password"
              type="password"
              fullWidth
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              sx={{ mb: 3 }}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={loading || !username || !password}
              sx={{ py: 1.2 }}
            >
              {loading ? 'Authenticating...' : 'Log In'}
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
