import { Alert, AlertTitle } from '@mui/material';
import { useDataSources } from '../api/hooks/useDataSources';

const LOW_BUDGET_THRESHOLD = 0.5;

export default function BackpressureIndicator() {
  const { data: dataSources } = useDataSources();

  if (!dataSources) return null;

  const affected = dataSources.filter(
    (ds) => ds.enabled && ds.samplingBudget < LOW_BUDGET_THRESHOLD,
  );

  if (affected.length === 0) return null;

  return (
    <Alert severity="warning" sx={{ mb: 2 }}>
      <AlertTitle>Backpressure Detected</AlertTitle>
      The following data sources have low sampling budgets:{' '}
      {affected.map((ds) => `${ds.name} (${Math.round(ds.samplingBudget * 100)}%)`).join(', ')}
    </Alert>
  );
}
