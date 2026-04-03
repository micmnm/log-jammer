import { useState } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import { useInviteRegister } from '../api/hooks/useInvites';
import { useNavigate, useSearchParams } from 'react-router-dom';

export default function Register() {
  const [searchParams] = useSearchParams();
  const inviteToken = searchParams.get('invite') ?? '';
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const register = useInviteRegister();
  const navigate = useNavigate();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    register.mutate(
      { token: inviteToken, username, displayName },
      { onSuccess: () => void navigate('/dashboard') }
    );
  }

  if (!inviteToken) {
    return (
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh', bgcolor: 'background.default' }}>
        <Card sx={{ width: 400, p: 2 }}>
          <CardContent>
            <Alert severity="error">No invite token provided. You need an invite link to register.</Alert>
          </CardContent>
        </Card>
      </Box>
    );
  }

  const canSubmit = username && displayName && !register.isPending;

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
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Create your account
          </Typography>

          {register.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {register.error instanceof Error ? register.error.message : 'Registration failed'}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit}>
            <TextField
              label="Username"
              fullWidth
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              sx={{ mb: 2 }}
              disabled={register.isPending}
              autoFocus
            />
            <TextField
              label="Display Name"
              fullWidth
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              sx={{ mb: 2 }}
              disabled={register.isPending}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={!canSubmit}
            >
              {register.isPending ? 'Registering…' : 'Register with Passkey'}
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
