import { createTheme } from '@mui/material/styles';

const theme = createTheme({
  palette: {
    mode: 'dark',
    background: { default: '#0a0e14', paper: '#0d1117' },
    primary: { main: '#00e5ff' },
    secondary: { main: '#ffb300' },
    error: { main: '#ff1744' },
    warning: { main: '#ff9100' },
    success: { main: '#00e676' },
  },
  typography: {
    fontFamily: "'IBM Plex Sans Condensed', 'Inter', 'Roboto', sans-serif",
    fontSize: 13,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: { backgroundColor: '#0a0e14' },
      },
    },
  },
});

export default theme;
