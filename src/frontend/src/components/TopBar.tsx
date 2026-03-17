import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import LogoutIcon from '@mui/icons-material/Logout';
import { useAuth } from '../api/hooks/useAuth';
import { useNavigate } from 'react-router-dom';

export default function TopBar() {
  const { logout } = useAuth();
  const navigate = useNavigate();

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
            fontWeight: 700,
            letterSpacing: '0.05em',
            color: 'primary.main',
          }}
        >
          LOG JAMMER
        </Typography>
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
