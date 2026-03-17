import { createTheme } from '@mui/material/styles';

const theme = createTheme({
  palette: {
    mode: 'dark',
    background: {
      default: '#0a0e14',
      paper: '#0f1520',
    },
    primary: {
      main: '#00bcd4',
    },
    secondary: {
      main: '#7986cb',
    },
    error: {
      main: '#f44336',
    },
    warning: {
      main: '#ff9800',
    },
    text: {
      primary: '#e0e6f0',
      secondary: '#8896a8',
    },
    divider: '#1e2d40',
  },
  typography: {
    fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
    monospace: '"JetBrains Mono", "Fira Code", "Consolas", monospace',
  } as never,
  components: {
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          border: '1px solid #1e2d40',
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        head: {
          color: '#8896a8',
          fontWeight: 600,
          fontSize: '0.75rem',
          textTransform: 'uppercase',
          letterSpacing: '0.05em',
          borderBottom: '1px solid #1e2d40',
        },
        body: {
          borderBottom: '1px solid #1e2d40',
        },
      },
    },
    MuiTableRow: {
      styleOverrides: {
        root: {
          '&:hover': {
            backgroundColor: 'rgba(0, 188, 212, 0.04)',
            cursor: 'pointer',
          },
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          fontWeight: 500,
        },
      },
    },
    MuiDrawer: {
      styleOverrides: {
        paper: {
          backgroundColor: '#070b10',
          border: 'none',
          borderRight: '1px solid #1e2d40',
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundColor: '#070b10',
          borderBottom: '1px solid #1e2d40',
          boxShadow: 'none',
        },
      },
    },
  },
});

export default theme;
