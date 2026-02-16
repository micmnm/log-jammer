import { Box, Card, CardContent, Typography, Grid, keyframes } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { useNavigate } from 'react-router-dom';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import LabelOffIcon from '@mui/icons-material/LabelOff';
import { useDashboardStats } from '../api/hooks/useDashboard';
import AlertsFeed from '../components/AlertsFeed';
import BackpressureIndicator from '../components/BackpressureIndicator';

const pulseGlow = keyframes`
  0%, 100% { box-shadow: 0 0 4px rgba(0, 229, 255, 0.2); }
  50% { box-shadow: 0 0 16px rgba(0, 229, 255, 0.5); }
`;

interface StatCardProps {
  title: string;
  value: number;
  icon: React.ReactNode;
  color: string;
  href?: string;
  pulse?: boolean;
}

function StatCard({ title, value, icon, color, href, pulse }: StatCardProps) {
  const theme = useTheme();
  const navigate = useNavigate();

  return (
    <Card
      variant="outlined"
      onClick={href ? () => navigate(href) : undefined}
      sx={{
        cursor: href ? 'pointer' : 'default',
        transition: 'box-shadow 0.2s ease',
        '&:hover': href
          ? { boxShadow: `0 0 12px ${color}40` }
          : undefined,
        ...(pulse
          ? { animation: `${pulseGlow} 2s ease-in-out infinite` }
          : {}),
      }}
    >
      <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Box sx={{ color, display: 'flex' }}>{icon}</Box>
        <Box>
          <Typography
            variant="h4"
            sx={{
              fontFamily: theme.fontFamilyMono,
              fontWeight: 700,
              letterSpacing: '0.02em',
            }}
          >
            {value}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {title}
          </Typography>
        </Box>
      </CardContent>
    </Card>
  );
}

export default function Dashboard() {
  const { firingCount, errorGroupCount, unclassifiedCount } = useDashboardStats();

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 3 }}>
        Dashboard
      </Typography>
      <Grid container spacing={2} sx={{ mb: 4 }}>
        <Grid size={{ xs: 12, sm: 4 }}>
          <StatCard
            title="Firing Alerts"
            value={firingCount}
            icon={<NotificationsActiveIcon fontSize="large" />}
            color="#ff1744"
            href="/alerts"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <StatCard
            title="Error Groups"
            value={errorGroupCount}
            icon={<ErrorOutlineIcon fontSize="large" />}
            color="#ff9100"
            href="/error-groups"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <StatCard
            title="Unclassified"
            value={unclassifiedCount}
            icon={<LabelOffIcon fontSize="large" />}
            color="#00e5ff"
            href="/classification"
            pulse={unclassifiedCount > 0}
          />
        </Grid>
      </Grid>
      <BackpressureIndicator />
      <AlertsFeed />
    </Box>
  );
}
