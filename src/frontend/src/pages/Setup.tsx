import { useState } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import { useSetupAdmin } from '../api/hooks/useSetup';
import { useNavigate } from 'react-router-dom';

export default function Setup() {
  const [token, setToken] = useState(() => {
    const params = new URLSearchParams(window.location.search);
    return params.get('token') ?? '';
  });
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const setup = useSetupAdmin();
  const navigate = useNavigate();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setup.mutate(
      { token, username, displayName },
      { onSuccess: () => void navigate('/dashboard') }
    );
  }

  const canSubmit = token && username && displayName && !setup.isPending;

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
      <Card sx={{ width: 420, p: 2 }}>
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
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Initial Setup
          </Typography>

          <Alert severity="info" sx={{ mb: 3 }}>
            This instance has not been set up yet. Check the application logs
            for the setup token, or paste it below to create the admin account.
          </Alert>

          {setup.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {setup.error instanceof Error ? setup.error.message : 'Setup failed'}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit}>
            <TextField
              label="Setup Token"
              fullWidth
              value={token}
              onChange={(e) => setToken(e.target.value)}
              sx={{ mb: 2 }}
              disabled={setup.isPending}
              helperText="From the application logs"
            />
            <TextField
              label="Username"
              fullWidth
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              sx={{ mb: 2 }}
              disabled={setup.isPending}
              autoFocus={!!token}
            />
            <TextField
              label="Display Name"
              fullWidth
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              sx={{ mb: 2 }}
              disabled={setup.isPending}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={!canSubmit}
            >
              {setup.isPending ? 'Setting up…' : 'Set Up Admin Account'}
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
