import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import FingerprintIcon from '@mui/icons-material/Fingerprint';
import { usePasskeyLogin } from '../api/hooks/useAuth';
import { useNavigate } from 'react-router-dom';

export default function Login() {
  const login = usePasskeyLogin();
  const navigate = useNavigate();

  function handleLogin() {
    login.mutate(undefined, {
      onSuccess: () => void navigate('/dashboard'),
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

          <Button
            variant="contained"
            fullWidth
            onClick={handleLogin}
            disabled={login.isPending}
            startIcon={<FingerprintIcon />}
            size="large"
          >
            {login.isPending ? 'Authenticating…' : 'Sign in with Passkey'}
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
}
