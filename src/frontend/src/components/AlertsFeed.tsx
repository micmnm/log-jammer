import { Box, Typography, CircularProgress, Alert } from '@mui/material';
import { useAlerts, useCorrelatedAlerts } from '../api/hooks/useAlerts';
import type { AlertDto } from '../api/types';
import AlertCard from './AlertCard';

const severityOrder: Record<string, number> = { Critical: 0, Warning: 1, Info: 2 };

function getSeverityScore(alert: AlertDto): number {
  if (alert.actualValue >= alert.thresholdValue * 2) return severityOrder['Critical'];
  if (alert.actualValue >= alert.thresholdValue) return severityOrder['Warning'];
  return severityOrder['Info'];
}

export default function AlertsFeed() {
  const { data, isLoading, error } = useAlerts('Firing');
  const { data: correlated } = useCorrelatedAlerts('Firing');

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return <Alert severity="error">Failed to load alerts: {error.message}</Alert>;
  }

  const alerts = [...(data?.items ?? [])].sort(
    (a, b) => getSeverityScore(a) - getSeverityScore(b)
  );

  const correlatedCount = correlated?.length ?? 0;

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
        <Typography variant="h6">Active Alerts</Typography>
        {correlatedCount > 0 && (
          <Typography variant="caption" color="warning.main">
            ({correlatedCount} correlated spike{correlatedCount !== 1 ? 's' : ''})
          </Typography>
        )}
      </Box>
      {alerts.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          No active alerts. All clear.
        </Typography>
      ) : (
        alerts.map((alert) => <AlertCard key={alert.id} alert={alert} />)
      )}
    </Box>
  );
}
