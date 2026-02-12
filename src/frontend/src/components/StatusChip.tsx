import { Chip } from '@mui/material';
import type { ErrorStatus } from '../api/types';

const statusConfig: Record<ErrorStatus, { color: 'primary' | 'success' | 'default' | 'info'; variant: 'filled' | 'outlined' }> = {
  Active: { color: 'primary', variant: 'filled' },
  Resolved: { color: 'success', variant: 'outlined' },
  Ignored: { color: 'default', variant: 'outlined' },
  Expected: { color: 'info', variant: 'outlined' },
};

interface StatusChipProps {
  status: ErrorStatus;
}

export default function StatusChip({ status }: StatusChipProps) {
  const config = statusConfig[status];
  return <Chip label={status} color={config.color} variant={config.variant} size="small" />;
}
