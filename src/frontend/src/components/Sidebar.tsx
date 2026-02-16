import {
  Box,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import DashboardIcon from '@mui/icons-material/Dashboard';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import LabelIcon from '@mui/icons-material/Label';
import StorageIcon from '@mui/icons-material/Storage';
import SettingsIcon from '@mui/icons-material/Settings';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import { useLocation, useNavigate } from 'react-router-dom';

const DRAWER_WIDTH = 240;
const DRAWER_WIDTH_COLLAPSED = 64;

const navItems = [
  {
    label: 'Dashboard',
    shortLabel: 'DASH',
    path: '/',
    icon: <DashboardIcon />,
    tooltip: 'The Big Picture',
    group: 'monitoring',
  },
  {
    label: 'Error Groups',
    shortLabel: 'ERRS',
    path: '/error-groups',
    icon: <ErrorOutlineIcon />,
    tooltip: 'Where bugs go to be catalogued',
    group: 'monitoring',
  },
  {
    label: 'Alerts',
    shortLabel: 'ALRT',
    path: '/alerts',
    icon: <NotificationsActiveIcon />,
    tooltip: 'Things that go BEEP in the night',
    group: 'monitoring',
  },
  {
    label: 'Classification',
    shortLabel: 'CLSF',
    path: '/classification',
    icon: <LabelIcon />,
    tooltip: 'Teaching robots to read errors',
    group: 'monitoring',
  },
  {
    label: 'Data Sources',
    shortLabel: 'SRC',
    path: '/data-sources',
    icon: <StorageIcon />,
    tooltip: 'Where the data flows',
    group: 'config',
  },
  {
    label: 'Settings',
    shortLabel: 'CONF',
    path: '/settings',
    icon: <SettingsIcon />,
    tooltip: "Don't touch anything... unless you want to",
    group: 'config',
  },
];

interface SidebarProps {
  open: boolean;
  collapsed: boolean;
  onClose: () => void;
  onToggleCollapse: () => void;
}

export default function Sidebar({ open, collapsed, onClose, onToggleCollapse }: SidebarProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const location = useLocation();
  const navigate = useNavigate();

  const isCollapsed = !isMobile && collapsed;
  const currentWidth = isCollapsed ? DRAWER_WIDTH_COLLAPSED : DRAWER_WIDTH;

  const handleNav = (path: string) => {
    navigate(path);
    if (isMobile) onClose();
  };

  const monitoringItems = navItems.filter((n) => n.group === 'monitoring');
  const configItems = navItems.filter((n) => n.group === 'config');

  const renderNavItem = (item: typeof navItems[number]) => {
    const isActive =
      item.path === '/'
        ? location.pathname === '/'
        : location.pathname.startsWith(item.path);

    const button = (
      <ListItemButton
        key={item.path}
        selected={false}
        onClick={() => handleNav(item.path)}
        sx={{
          mx: isCollapsed ? 0.5 : 1,
          borderRadius: 1,
          borderLeft: isActive ? '3px solid' : '3px solid transparent',
          borderLeftColor: isActive ? 'primary.main' : 'transparent',
          backgroundColor: 'transparent',
          pl: isCollapsed ? 1.5 : 2,
          flexDirection: isCollapsed ? 'column' : 'row',
          alignItems: 'center',
          py: isCollapsed ? 1.2 : 0.8,
          minHeight: isCollapsed ? 56 : 'auto',
          '&:hover': {
            backgroundColor: 'rgba(0, 229, 255, 0.06)',
          },
        }}
      >
        <ListItemIcon
          sx={{
            minWidth: isCollapsed ? 'auto' : 40,
            color: isActive ? 'primary.main' : 'text.secondary',
            justifyContent: 'center',
          }}
        >
          {item.icon}
        </ListItemIcon>
        {isCollapsed ? (
          <Typography
            variant="caption"
            sx={{
              fontSize: '0.6rem',
              fontFamily: theme.fontFamilyMono,
              fontWeight: 500,
              color: isActive ? 'primary.main' : 'text.secondary',
              mt: 0.3,
              letterSpacing: '0.05em',
            }}
          >
            {item.shortLabel}
          </Typography>
        ) : (
          <ListItemText
            primary={item.label}
            secondary={item.shortLabel}
            slotProps={{
              primary: {
                sx: {
                  color: isActive ? 'primary.main' : 'text.primary',
                  fontWeight: isActive ? 600 : 400,
                },
              },
              secondary: {
                sx: {
                  fontFamily: theme.fontFamilyMono,
                  fontSize: '0.6rem',
                  letterSpacing: '0.08em',
                  color: 'text.secondary',
                  opacity: 0.6,
                },
              },
            }}
          />
        )}
      </ListItemButton>
    );

    if (isCollapsed) {
      return (
        <Tooltip key={item.path} title={item.tooltip} placement="right" arrow>
          {button}
        </Tooltip>
      );
    }

    return (
      <Tooltip key={item.path} title={item.tooltip} placement="right" arrow>
        {button}
      </Tooltip>
    );
  };

  const drawerContent = (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <Toolbar />
      <List sx={{ flex: 1, pt: 1 }}>
        {monitoringItems.map(renderNavItem)}
        <Divider sx={{ my: 1, mx: isCollapsed ? 1 : 2, borderColor: 'rgba(0, 229, 255, 0.08)' }} />
        {configItems.map(renderNavItem)}
      </List>

      {/* Collapse toggle */}
      {!isMobile && (
        <Box sx={{ borderTop: '1px solid rgba(0, 229, 255, 0.08)' }}>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: isCollapsed ? 'center' : 'space-between',
              px: isCollapsed ? 0 : 2,
              py: 1,
            }}
          >
            {!isCollapsed && (
              <Typography
                variant="caption"
                sx={{
                  fontFamily: theme.fontFamilyMono,
                  fontSize: '0.6rem',
                  color: 'text.secondary',
                  opacity: 0.5,
                  letterSpacing: '0.1em',
                }}
              >
                v0.1.0
              </Typography>
            )}
            <IconButton size="small" onClick={onToggleCollapse} sx={{ color: 'text.secondary' }}>
              {isCollapsed ? <ChevronRightIcon fontSize="small" /> : <ChevronLeftIcon fontSize="small" />}
            </IconButton>
          </Box>
        </Box>
      )}
    </Box>
  );

  return (
    <Drawer
      variant={isMobile ? 'temporary' : 'persistent'}
      open={open}
      onClose={onClose}
      sx={{
        width: open ? currentWidth : 0,
        flexShrink: 0,
        '& .MuiDrawer-paper': {
          width: currentWidth,
          boxSizing: 'border-box',
          transition: theme.transitions.create('width', {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.enteringScreen,
          }),
          overflowX: 'hidden',
        },
      }}
    >
      {drawerContent}
    </Drawer>
  );
}

export { DRAWER_WIDTH, DRAWER_WIDTH_COLLAPSED };
