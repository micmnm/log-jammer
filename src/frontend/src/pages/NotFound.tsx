import { useState, useEffect } from 'react';
import { Box, Typography, Button } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { useNavigate } from 'react-router-dom';

const MESSAGES = [
  'This page has been decomissioned.',
  'The logs here have been... misplaced.',
  'Error 404: Page not found in any data source.',
  'We checked all the indices. Nothing.',
  'This route was last seen heading north.',
  'Even our ML model couldn\'t classify this.',
  'The packets went that way. Probably.',
  'Have you tried turning the URL off and on again?',
];

export default function NotFound() {
  const theme = useTheme();
  const navigate = useNavigate();
  const [dots, setDots] = useState('');
  const [message] = useState(() => MESSAGES[Math.floor(Math.random() * MESSAGES.length)]);

  useEffect(() => {
    const interval = setInterval(() => {
      setDots((d) => (d.length >= 3 ? '' : d + '.'));
    }, 500);
    return () => clearInterval(interval);
  }, []);

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '70vh',
        textAlign: 'center',
        px: 2,
      }}
    >
      <Typography
        variant="h1"
        sx={{
          fontFamily: theme.fontFamilyMono,
          fontWeight: 700,
          fontSize: { xs: '6rem', md: '10rem' },
          color: 'primary.main',
          textShadow: '0 0 20px rgba(0, 229, 255, 0.4), 0 0 40px rgba(0, 229, 255, 0.2)',
          lineHeight: 1,
          mb: 2,
          animation: 'glitch404 3s infinite',
          '@keyframes glitch404': {
            '0%, 95%, 100%': { textShadow: '0 0 20px rgba(0, 229, 255, 0.4), 0 0 40px rgba(0, 229, 255, 0.2)' },
            '96%': { textShadow: '-2px 0 #ff1744, 2px 0 #00e5ff', transform: 'translate(2px, 0)' },
            '97%': { textShadow: '2px 0 #ff1744, -2px 0 #00e5ff', transform: 'translate(-2px, 0)' },
            '98%': { textShadow: '-1px 0 #ff1744, 1px 0 #00e5ff', transform: 'translate(1px, 0)' },
          },
        }}
      >
        404
      </Typography>

      <Typography
        variant="h4"
        sx={{
          fontFamily: theme.fontFamilyMono,
          fontWeight: 600,
          letterSpacing: '0.2em',
          color: 'error.main',
          mb: 3,
          textTransform: 'uppercase',
        }}
      >
        SIGNAL LOST
      </Typography>

      <Typography
        variant="body1"
        sx={{
          fontFamily: theme.fontFamilyMono,
          color: 'text.secondary',
          mb: 1,
          fontSize: '0.85rem',
        }}
      >
        {message}
      </Typography>

      <Typography
        variant="body2"
        sx={{
          fontFamily: theme.fontFamilyMono,
          color: 'text.secondary',
          opacity: 0.5,
          mb: 4,
          minWidth: 200,
        }}
      >
        scanning for route{dots}
      </Typography>

      <Button
        onClick={() => navigate('/')}
        sx={{
          fontFamily: theme.fontFamilyMono,
          fontWeight: 500,
          color: 'primary.main',
          border: '1px solid rgba(0, 229, 255, 0.3)',
          borderRadius: 1,
          px: 3,
          py: 1,
          textTransform: 'none',
          letterSpacing: '0.05em',
          '&:hover': {
            backgroundColor: 'rgba(0, 229, 255, 0.08)',
            borderColor: 'primary.main',
          },
        }}
      >
        {'> navigate --home'}
      </Button>
    </Box>
  );
}
