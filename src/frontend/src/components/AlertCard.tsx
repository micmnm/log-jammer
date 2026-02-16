import {
  Card,
  CardContent,
  Typography,
  Chip,
  Box,
  Button,
  Stack,
} from '@mui/material';
import type { AlertDto } from '../api/types';
import { useAcknowledgeAlert } from '../api/hooks/useAlerts';

const severityColors: Record<string, 'error' | 'warning' | 'info'> = {
  Critical: 'error',
  Warning: 'warning',
  Info: 'info',
};

function getSeverityFromThreshold(alert: AlertDto): string {
  if (alert.actualValue >= alert.thresholdValue * 2) return 'Critical';
  if (alert.actualValue >= alert.thresholdValue) return 'Warning';
  return 'Info';
}

function timeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

interface AlertCardProps {
  alert: AlertDto;
  showAcknowledge?: boolean;
}

export default function AlertCard({ alert, showAcknowledge = true }: AlertCardProps) {
  const acknowledge = useAcknowledgeAlert();
  const severity = getSeverityFromThreshold(alert);
  const color = severityColors[severity] ?? 'info';
  const isCritical = severity === 'Critical';

  return (
    <Card
      variant="outlined"
      sx={{
        mb: 1.5,
        ...(isCritical && {
          borderLeft: '3px solid',
          borderLeftColor: 'error.main',
          animation: 'alertPulse 2s ease-in-out infinite',
          '@keyframes alertPulse': {
            '0%, 100%': { boxShadow: '0 0 4px rgba(255, 23, 68, 0.1)' },
            '50%': { boxShadow: '0 0 12px rgba(255, 23, 68, 0.2), -4px 0 16px rgba(255, 23, 68, 0.1)' },
          },
        }),
      }}
    >
      <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
        <Stack direction="row" alignItems="flex-start" justifyContent="space-between" spacing={2}>
          <Box sx={{ minWidth: 0, flex: 1 }}>
            <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 0.5 }}>
              <Chip label={severity} color={color} size="small" />
              <Chip label={alert.status} variant="outlined" size="small" />
              <Typography variant="caption" color="text.secondary">
                {timeAgo(alert.createdAt)}
              </Typography>
            </Stack>
            <Typography variant="body2" sx={{ wordBreak: 'break-word' }}>
              {alert.knownErrorMessage ?? 'Unknown error'}
            </Typography>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ fontFamily: (theme) => theme.fontFamilyMono, fontSize: '0.7rem' }}
            >
              Threshold: {alert.thresholdType} {alert.thresholdValue} | Actual: {alert.actualValue}
            </Typography>
          </Box>
          {showAcknowledge && alert.status === 'Firing' && (
            <Button
              size="small"
              variant="outlined"
              color="warning"
              onClick={() => acknowledge.mutate(alert.id)}
              disabled={acknowledge.isPending}
              sx={{ flexShrink: 0 }}
            >
              Ack
            </Button>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}
