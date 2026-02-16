import { useState } from 'react';
import { Box, Toolbar, useMediaQuery, useTheme } from '@mui/material';
import { Outlet } from 'react-router-dom';
import Sidebar, { DRAWER_WIDTH, DRAWER_WIDTH_COLLAPSED } from './Sidebar';
import TopBar from './TopBar';
import { useKonamiCode } from '../hooks/useKonamiCode';

export default function Layout() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [sidebarOpen, setSidebarOpen] = useState(!isMobile);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const { active: crtActive } = useKonamiCode();

  const toggleSidebar = () => setSidebarOpen((prev) => !prev);
  const toggleCollapse = () => setSidebarCollapsed((prev) => !prev);

  const effectiveWidth = sidebarOpen && !isMobile
    ? sidebarCollapsed ? DRAWER_WIDTH_COLLAPSED : DRAWER_WIDTH
    : 0;

  return (
    <Box
      sx={{
        display: 'flex',
        minHeight: '100vh',
        // Subtle grid background texture
        backgroundImage:
          'linear-gradient(rgba(0, 229, 255, 0.02) 1px, transparent 1px), linear-gradient(90deg, rgba(0, 229, 255, 0.02) 1px, transparent 1px)',
        backgroundSize: '20px 20px',
        // CRT effect
        ...(crtActive && {
          filter: 'blur(0.3px) hue-rotate(80deg) brightness(0.95)',
          transition: 'filter 0.5s ease',
        }),
        ...(!crtActive && {
          transition: 'filter 0.5s ease',
        }),
      }}
    >
      {/* CRT scanline overlay */}
      {crtActive && (
        <Box
          sx={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            pointerEvents: 'none',
            zIndex: 9999,
            backgroundImage:
              'repeating-linear-gradient(0deg, rgba(0, 0, 0, 0.15) 0px, rgba(0, 0, 0, 0.15) 1px, transparent 1px, transparent 3px)',
            borderRadius: '8px',
            animation: 'crtFade 8s ease-out forwards',
            '@keyframes crtFade': {
              '0%': { opacity: 1 },
              '80%': { opacity: 1 },
              '100%': { opacity: 0 },
            },
          }}
        />
      )}

      <TopBar
        sidebarOpen={sidebarOpen && !isMobile}
        sidebarCollapsed={sidebarCollapsed}
        onToggleSidebar={toggleSidebar}
      />
      <Sidebar
        open={sidebarOpen}
        collapsed={sidebarCollapsed}
        onClose={() => setSidebarOpen(false)}
        onToggleCollapse={toggleCollapse}
      />
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          p: 3,
          width: `calc(100% - ${effectiveWidth}px)`,
          transition: theme.transitions.create(['width', 'margin'], {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.leavingScreen,
          }),
        }}
      >
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  );
}
