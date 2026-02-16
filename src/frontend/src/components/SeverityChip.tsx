import { Chip } from '@mui/material';
import type { ErrorSeverity } from '../api/types';

const severityColors: Record<ErrorSeverity, 'error' | 'warning' | 'info'> = {
  Critical: 'error',
  Warning: 'warning',
  Info: 'info',
};

interface SeverityChipProps {
  severity: ErrorSeverity;
}

export default function SeverityChip({ severity }: SeverityChipProps) {
  return (
    <Chip
      label={severity}
      color={severityColors[severity]}
      size="small"
      sx={
        severity === 'Critical'
          ? {
              animation: 'severityPulse 2s ease-in-out infinite',
              '@keyframes severityPulse': {
                '0%, 100%': { boxShadow: '0 0 4px rgba(255, 23, 68, 0.3)' },
                '50%': { boxShadow: '0 0 12px rgba(255, 23, 68, 0.6), 0 0 20px rgba(255, 23, 68, 0.2)' },
              },
            }
          : undefined
      }
    />
  );
}
