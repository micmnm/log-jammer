import { useState } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import { usePasskeyLogin } from '../api/hooks/useAuth';
import { useNavigate } from 'react-router-dom';

export default function Login() {
  const [password, setPassword] = useState('');
  const login = usePasskeyLogin();
  const navigate = useNavigate();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    login.mutate(undefined, {
      onSuccess: () => {
        void navigate('/dashboard');
      },
    });
  }

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '100vh',
        bgcolor: 'background.default',
      }}
    >
      <Card sx={{ width: 360, p: 2 }}>
        <CardContent>
          <Typography
            variant="h5"
            component="h1"
            sx={{
              mb: 1,
              fontFamily: '"Lexend", sans-serif',
              fontWeight: 700,
              letterSpacing: '0.05em',
              color: 'primary.main',
            }}
          >
            Log Jammer
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Log monitoring & anomaly detection
          </Typography>

          {login.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {login.error instanceof Error ? login.error.message : 'Login failed'}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit}>
            <TextField
              label="Password"
              type="password"
              fullWidth
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoFocus
              sx={{ mb: 2 }}
              disabled={login.isPending}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={login.isPending || !password}
            >
              {login.isPending ? 'Signing in…' : 'Sign In'}
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
