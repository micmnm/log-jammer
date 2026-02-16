import { Box, Card, CardContent, Typography, Grid } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import LabelOffIcon from '@mui/icons-material/LabelOff';
import { useDashboardStats } from '../api/hooks/useDashboard';
import AlertsFeed from '../components/AlertsFeed';
import BackpressureIndicator from '../components/BackpressureIndicator';

interface StatCardProps {
  title: string;
  value: number;
  icon: React.ReactNode;
  color: string;
}

function StatCard({ title, value, icon, color }: StatCardProps) {
  const theme = useTheme();
  return (
    <Card variant="outlined">
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
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <StatCard
            title="Error Groups"
            value={errorGroupCount}
            icon={<ErrorOutlineIcon fontSize="large" />}
            color="#ff9100"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <StatCard
            title="Unclassified"
            value={unclassifiedCount}
            icon={<LabelOffIcon fontSize="large" />}
            color="#00e5ff"
          />
        </Grid>
      </Grid>
      <BackpressureIndicator />
      <AlertsFeed />
    </Box>
  );
}
