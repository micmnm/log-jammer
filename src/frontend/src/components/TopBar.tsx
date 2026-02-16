import { useState, useEffect, useCallback, useRef } from 'react';
import { AppBar, Box, IconButton, Toolbar, Typography } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import MenuIcon from '@mui/icons-material/Menu';
import FiberManualRecordIcon from '@mui/icons-material/FiberManualRecord';
import { DRAWER_WIDTH, DRAWER_WIDTH_COLLAPSED } from './Sidebar';

const STATUS_QUIPS = [
  'ALL SYSTEMS NOMINAL',
  'COFFEE LEVELS: CRITICAL',
  'NO INCIDENTS... SUSPICIOUS',
  'UPTIME: YES',
  'LOGS: STILL LOGGING',
  'ENTROPY: INCREASING',
  'PACKETS: FLOWING',
  'USERS: PROBABLY FINE',
  'LATENCY: ACCEPTABLE',
  'DISK SPACE: HOLDING',
  'MEMORY: WHO AM I?',
  'CPU: THINKING HARD',
  'ERRORS: UNDER REVIEW',
  'NETWORK: TUBES CLEAR',
];

interface TopBarProps {
  sidebarOpen: boolean;
  sidebarCollapsed: boolean;
  onToggleSidebar: () => void;
}

export default function TopBar({ sidebarOpen, sidebarCollapsed, onToggleSidebar }: TopBarProps) {
  const theme = useTheme();
  const [time, setTime] = useState(new Date());
  const [colonVisible, setColonVisible] = useState(true);
  const [quipIndex, setQuipIndex] = useState(0);
  const [quipFading, setQuipFading] = useState(false);
  const [glitching, setGlitching] = useState(false);
  const clickTimesRef = useRef<number[]>([]);

  // Clock tick
  useEffect(() => {
    const interval = setInterval(() => {
      setTime(new Date());
      setColonVisible((v) => !v);
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  // Rotating quips
  useEffect(() => {
    const interval = setInterval(() => {
      setQuipFading(true);
      setTimeout(() => {
        setQuipIndex((i) => (i + 1) % STATUS_QUIPS.length);
        setQuipFading(false);
      }, 300);
    }, 15000);
    return () => clearInterval(interval);
  }, []);

  // Title click counter → glitch
  const handleTitleClick = useCallback(() => {
    const now = Date.now();
    clickTimesRef.current = [...clickTimesRef.current.filter((t) => now - t < 3000), now];
    if (clickTimesRef.current.length >= 10) {
      setGlitching(true);
      clickTimesRef.current = [];
      setTimeout(() => setGlitching(false), 1000);
    }
  }, []);

  const hours = time.getHours().toString().padStart(2, '0');
  const minutes = time.getMinutes().toString().padStart(2, '0');
  const seconds = time.getSeconds().toString().padStart(2, '0');
  const colon = colonVisible ? ':' : '\u00A0';

  const sidebarWidth = sidebarOpen
    ? sidebarCollapsed ? DRAWER_WIDTH_COLLAPSED : DRAWER_WIDTH
    : 0;

  return (
    <AppBar
      position="fixed"
      sx={{
        zIndex: (theme) => theme.zIndex.drawer + 1,
        width: `calc(100% - ${sidebarWidth}px)`,
        ml: `${sidebarWidth}px`,
        transition: (theme) =>
          theme.transitions.create(['width', 'margin'], {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.leavingScreen,
          }),
        backgroundColor: 'background.paper',
      }}
    >
      <Toolbar sx={{ justifyContent: 'space-between' }}>
        <Box sx={{ display: 'flex', alignItems: 'center' }}>
          <IconButton
            color="inherit"
            edge="start"
            onClick={onToggleSidebar}
            sx={{ mr: 2 }}
          >
            <MenuIcon />
          </IconButton>
          <Typography
            variant="h6"
            noWrap
            onClick={handleTitleClick}
            sx={{
              fontFamily: theme.fontFamilyMono,
              fontWeight: 700,
              color: 'primary.main',
              textShadow: '0 0 10px rgba(0, 229, 255, 0.5), 0 0 20px rgba(0, 229, 255, 0.2)',
              cursor: 'default',
              userSelect: 'none',
              letterSpacing: '0.05em',
              ...(glitching && {
                animation: 'glitch 0.15s infinite',
                '@keyframes glitch': {
                  '0%': { clipPath: 'inset(40% 0 61% 0)', transform: 'translate(-2px, 2px)' },
                  '20%': { clipPath: 'inset(92% 0 1% 0)', transform: 'translate(2px, -1px)' },
                  '40%': { clipPath: 'inset(43% 0 1% 0)', transform: 'translate(-1px, 3px)' },
                  '60%': { clipPath: 'inset(25% 0 58% 0)', transform: 'translate(3px, 1px)' },
                  '80%': { clipPath: 'inset(54% 0 7% 0)', transform: 'translate(-3px, -2px)' },
                  '100%': { clipPath: 'inset(58% 0 43% 0)', transform: 'translate(2px, 2px)' },
                },
              }),
            }}
          >
            LOG JAMMER
          </Typography>
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          {/* Status quip */}
          <Typography
            variant="caption"
            sx={{
              fontFamily: theme.fontFamilyMono,
              color: 'text.secondary',
              opacity: quipFading ? 0 : 0.7,
              transition: 'opacity 0.3s ease',
              display: { xs: 'none', md: 'block' },
              letterSpacing: '0.08em',
              fontSize: '0.65rem',
            }}
          >
            {STATUS_QUIPS[quipIndex]}
          </Typography>

          {/* Status dot */}
          <FiberManualRecordIcon
            sx={{
              fontSize: 8,
              color: 'success.main',
              filter: 'drop-shadow(0 0 3px rgba(0, 230, 118, 0.6))',
              display: { xs: 'none', sm: 'block' },
            }}
          />

          {/* Clock */}
          <Typography
            variant="body2"
            sx={{
              fontFamily: theme.fontFamilyMono,
              fontWeight: 500,
              letterSpacing: '0.05em',
              color: 'text.primary',
              minWidth: 75,
              textAlign: 'right',
            }}
          >
            {hours}{colon}{minutes}{colon}{seconds}
          </Typography>

          {/* Blinking cursor */}
          <Typography
            variant="body2"
            sx={{
              fontFamily: theme.fontFamilyMono,
              color: 'primary.main',
              animation: 'blink 1s step-end infinite',
              '@keyframes blink': {
                '0%, 100%': { opacity: 1 },
                '50%': { opacity: 0 },
              },
            }}
          >
            _
          </Typography>
        </Box>
      </Toolbar>
    </AppBar>
  );
}
