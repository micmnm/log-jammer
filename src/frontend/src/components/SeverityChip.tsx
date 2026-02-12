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
  return <Chip label={severity} color={severityColors[severity]} size="small" />;
}
