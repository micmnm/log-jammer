import { createTheme } from '@mui/material/styles';
import type {} from '@mui/x-data-grid/themeAugmentation';

declare module '@mui/material/styles' {
  interface Theme {
    fontFamilyMono: string;
  }
  interface ThemeOptions {
    fontFamilyMono?: string;
  }
}

const FONT_MONO = "'JetBrains Mono', 'Fira Code', 'Cascadia Code', monospace";
const FONT_UI = "'IBM Plex Sans Condensed', 'Inter', 'Roboto', sans-serif";

const theme = createTheme({
  fontFamilyMono: FONT_MONO,
  palette: {
    mode: 'dark',
    primary: {
      main: '#00e5ff',
      light: '#6effff',
      dark: '#00b2cc',
    },
    secondary: {
      main: '#ffb300',
      light: '#ffe54c',
      dark: '#c68400',
    },
    background: {
      default: '#0a0e14',
      paper: '#0d1117',
    },
    error: {
      main: '#ff1744',
    },
    warning: {
      main: '#ff9100',
    },
    info: {
      main: '#00e5ff',
    },
    success: {
      main: '#00e676',
    },
    divider: 'rgba(0, 229, 255, 0.08)',
    text: {
      primary: '#e6edf3',
      secondary: '#8b949e',
    },
  },
  typography: {
    fontFamily: FONT_UI,
    h4: { fontWeight: 600 },
    h5: { fontWeight: 600 },
    h6: { fontWeight: 600 },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          scrollbarColor: '#1a2233 #0a0e14',
          '&::-webkit-scrollbar': { width: 8 },
          '&::-webkit-scrollbar-track': { background: '#0a0e14' },
          '&::-webkit-scrollbar-thumb': {
            background: '#1a2233',
            borderRadius: 4,
          },
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          borderRadius: 4,
          border: '1px solid rgba(0, 229, 255, 0.1)',
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        root: {
          borderBottom: '1px solid rgba(0, 229, 255, 0.06)',
        },
        head: {
          fontWeight: 600,
          textTransform: 'uppercase' as const,
          fontSize: '0.75rem',
          letterSpacing: '0.05em',
          color: '#8b949e',
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 2,
          textTransform: 'uppercase' as const,
          fontWeight: 600,
          letterSpacing: '0.04em',
        },
        contained: {
          boxShadow: 'none',
          '&:hover': {
            boxShadow: 'none',
          },
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          borderRadius: 4,
          fontWeight: 500,
        },
      },
    },
    MuiDrawer: {
      styleOverrides: {
        paper: {
          borderRight: '2px solid rgba(0, 229, 255, 0.15)',
          backgroundImage: 'none',
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          boxShadow: 'none',
          borderBottom: '1px solid rgba(0, 229, 255, 0.08)',
        },
      },
    },
    MuiDataGrid: {
      styleOverrides: {
        root: {
          border: '1px solid rgba(0, 229, 255, 0.1)',
          '& .MuiDataGrid-columnHeaders': {
            backgroundColor: 'rgba(0, 229, 255, 0.04)',
          },
          '& .MuiDataGrid-row:hover': {
            backgroundColor: 'rgba(0, 229, 255, 0.04)',
          },
          '& .MuiDataGrid-cell': {
            borderBottom: '1px solid rgba(0, 229, 255, 0.06)',
          },
        },
      },
    },
    MuiAccordion: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          '&:before': { display: 'none' },
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          backgroundImage: 'none',
          border: '1px solid rgba(0, 229, 255, 0.1)',
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
        },
      },
    },
  },
});

export default theme;
