import { Box, Card, CardContent, Typography, Grid } from '@mui/material';
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
  return (
    <Card variant="outlined">
      <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Box sx={{ color, display: 'flex' }}>{icon}</Box>
        <Box>
          <Typography variant="h4">{value}</Typography>
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
            color="#f44336"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <StatCard
            title="Error Groups"
            value={errorGroupCount}
            icon={<ErrorOutlineIcon fontSize="large" />}
            color="#ff9800"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <StatCard
            title="Unclassified"
            value={unclassifiedCount}
            icon={<LabelOffIcon fontSize="large" />}
            color="#5c9ce6"
          />
        </Grid>
      </Grid>
      <BackpressureIndicator />
      <AlertsFeed />
    </Box>
  );
}
