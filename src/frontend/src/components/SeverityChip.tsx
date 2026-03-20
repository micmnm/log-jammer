import Chip from '@mui/material/Chip';
import type { Severity } from '../api/types';

interface SeverityChipProps {
  severity: Severity;
}

export default function SeverityChip({ severity }: SeverityChipProps) {
  switch (severity) {
    case 'Critical':
      return <Chip label="Critical" color="error" size="small" variant="filled" />;
    case 'Error':
      return <Chip label="Error" color="error" size="small" variant="outlined" />;
    case 'Warning':
      return <Chip label="Warning" color="warning" size="small" variant="outlined" />;
    case 'Info':
    default:
      return <Chip label="Info" size="small" variant="outlined" />;
  }
}
