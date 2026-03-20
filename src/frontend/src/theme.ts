import { createTheme, type ThemeOptions } from '@mui/material/styles';

const shared: ThemeOptions = {
  typography: {
    fontFamily: '"Nunito Sans", "Helvetica", "Arial", sans-serif',
    monospace: '"JetBrains Mono", "Fira Code", "Consolas", monospace',
    h1: { fontFamily: '"Lexend", sans-serif', fontWeight: 700 },
    h2: { fontFamily: '"Lexend", sans-serif', fontWeight: 700 },
    h3: { fontFamily: '"Lexend", sans-serif', fontWeight: 600 },
    h4: { fontFamily: '"Lexend", sans-serif', fontWeight: 600 },
    h5: { fontFamily: '"Lexend", sans-serif', fontWeight: 600 },
    h6: { fontFamily: '"Lexend", sans-serif', fontWeight: 600 },
    subtitle1: { fontFamily: '"Lexend", sans-serif', fontWeight: 600 },
    subtitle2: { fontFamily: '"Lexend", sans-serif', fontWeight: 500 },
  } as never,
  shape: { borderRadius: 12 },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { textTransform: 'none', fontWeight: 500, borderRadius: 8 },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        head: {
          fontWeight: 600,
          fontSize: '0.75rem',
          textTransform: 'uppercase',
          letterSpacing: '0.05em',
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: { borderRadius: 8 },
      },
    },
  },
};

export const darkTheme = createTheme({
  ...shared,
  palette: {
    mode: 'dark',
    background: {
      default: '#111318',
      paper: '#1a1d24',
    },
    primary: {
      main: '#6ec6d6',
      light: '#8dd4e0',
      dark: '#4a9aaa',
    },
    secondary: {
      main: '#a3b1d6',
    },
    error: {
      main: '#e57373',
    },
    warning: {
      main: '#ffb74d',
    },
    success: {
      main: '#81c784',
    },
    text: {
      primary: '#d8dce6',
      secondary: '#8a93a8',
    },
    divider: 'rgba(140, 160, 190, 0.12)',
  },
  components: {
    ...shared.components,
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          border: '1px solid rgba(140, 160, 190, 0.12)',
        },
      },
    },
    MuiTableRow: {
      styleOverrides: {
        root: {
          '&:hover': {
            backgroundColor: 'rgba(110, 198, 214, 0.06)',
          },
        },
      },
    },
    MuiDrawer: {
      styleOverrides: {
        paper: {
          backgroundColor: '#15171e',
          border: 'none',
          borderRight: '1px solid rgba(140, 160, 190, 0.12)',
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundColor: '#15171e',
          borderBottom: '1px solid rgba(140, 160, 190, 0.12)',
          boxShadow: 'none',
        },
      },
    },
  },
});

export const lightTheme = createTheme({
  ...shared,
  palette: {
    mode: 'light',
    background: {
      default: '#f5f6fa',
      paper: '#fafbfd',
    },
    primary: {
      main: '#3d8c9e',
      light: '#5ba8b8',
      dark: '#2d6b78',
    },
    secondary: {
      main: '#6b7aab',
    },
    error: {
      main: '#d45050',
    },
    warning: {
      main: '#d48c30',
    },
    success: {
      main: '#4a9c4e',
    },
    text: {
      primary: '#2a2f3a',
      secondary: '#6b7386',
    },
    divider: 'rgba(0, 0, 0, 0.08)',
  },
  components: {
    ...shared.components,
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          border: '1px solid rgba(0, 0, 0, 0.08)',
          boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
        },
      },
    },
    MuiTableRow: {
      styleOverrides: {
        root: {
          '&:hover': {
            backgroundColor: 'rgba(61, 140, 158, 0.04)',
          },
        },
      },
    },
    MuiDrawer: {
      styleOverrides: {
        paper: {
          backgroundColor: '#fafbfd',
          border: 'none',
          borderRight: '1px solid rgba(0, 0, 0, 0.08)',
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundColor: '#fafbfd',
          borderBottom: '1px solid rgba(0, 0, 0, 0.08)',
          boxShadow: 'none',
        },
      },
    },
  },
});
