import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import LogoutIcon from '@mui/icons-material/Logout';
import LightModeIcon from '@mui/icons-material/LightMode';
import DarkModeIcon from '@mui/icons-material/DarkMode';
import SettingsBrightnessIcon from '@mui/icons-material/SettingsBrightness';
import { useAuth } from '../api/hooks/useAuth';
import { useThemeMode } from '../ThemeContext';
import { useNavigate } from 'react-router-dom';

const modeSequence = ['system', 'light', 'dark'] as const;

export default function TopBar() {
  const { logout } = useAuth();
  const navigate = useNavigate();
  const { mode, setMode } = useThemeMode();

  function cycleMode() {
    const idx = modeSequence.indexOf(mode);
    setMode(modeSequence[(idx + 1) % modeSequence.length]);
  }

  const modeIcon = mode === 'light'
    ? <LightModeIcon fontSize="small" />
    : mode === 'dark'
      ? <DarkModeIcon fontSize="small" />
      : <SettingsBrightnessIcon fontSize="small" />;

  const modeLabel = mode === 'system' ? 'System' : mode === 'light' ? 'Light' : 'Dark';

  function handleLogout() {
    logout();
    void navigate('/');
  }

  return (
    <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
      <Toolbar>
        <Typography
          variant="h6"
          component="div"
          sx={{
            flexGrow: 1,
            fontFamily: '"Lexend", sans-serif',
            fontWeight: 700,
            letterSpacing: '0.04em',
            color: 'primary.main',
          }}
        >
          Log Jammer
        </Typography>
        <Tooltip title={`Theme: ${modeLabel}`}>
          <IconButton onClick={cycleMode} sx={{ color: 'text.secondary', mr: 1 }}>
            {modeIcon}
          </IconButton>
        </Tooltip>
        <Button
          color="inherit"
          onClick={handleLogout}
          startIcon={<LogoutIcon />}
          size="small"
          sx={{ color: 'text.secondary' }}
        >
          Logout
        </Button>
      </Toolbar>
    </AppBar>
  );
}
