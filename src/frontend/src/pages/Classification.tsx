import { useMemo, useState } from 'react';
import {
  Box,
  Typography,
  Pagination,
  CircularProgress,
  Alert,
  ToggleButton,
  ToggleButtonGroup,
} from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { useClassificationQueue } from '../api/hooks/useClassification';
import ClassificationQueueCard from '../components/ClassificationQueueCard';
import type { ClassificationQueueResponse } from '../api/types';

type ConfidenceBand = 'all' | 'high' | 'medium' | 'low';

function getConfidenceBand(confidence: number | null): Exclude<ConfidenceBand, 'all'> | null {
  if (confidence == null) return null;
  if (confidence >= 0.7) return 'high';
  if (confidence >= 0.4) return 'medium';
  return 'low';
}

function matchesBand(item: ClassificationQueueResponse, band: ConfidenceBand): boolean {
  if (band === 'all') return true;
  return getConfidenceBand(item.confidence) === band;
}

interface StatBoxProps {
  label: string;
  value: string | number;
  color: string;
}

function StatBox({ label, value, color }: StatBoxProps) {
  const theme = useTheme();
  return (
    <Box
      sx={{
        px: 2,
        py: 1,
        border: '1px solid rgba(255,255,255,0.08)',
        borderRadius: 1,
        minWidth: 100,
        textAlign: 'center',
      }}
    >
      <Typography
        variant="h6"
        sx={{
          fontFamily: theme.fontFamilyMono,
          fontWeight: 700,
          color,
          lineHeight: 1.2,
        }}
      >
        {value}
      </Typography>
      <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: '0.05em', fontSize: '0.65rem' }}>
        {label}
      </Typography>
    </Box>
  );
}

export default function Classification() {
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState<ConfidenceBand>('all');
  const pageSize = 10;

  const { data, isLoading, error } = useClassificationQueue(page, pageSize);

  const stats = useMemo(() => {
    if (!data) return null;
    const items = data.items;
    let high = 0;
    let medium = 0;
    let low = 0;
    let sum = 0;
    let count = 0;

    for (const item of items) {
      const band = getConfidenceBand(item.confidence);
      if (band === 'high') high++;
      else if (band === 'medium') medium++;
      else if (band === 'low') low++;

      if (item.confidence != null) {
        sum += item.confidence;
        count++;
      }
    }

    return {
      totalPending: data.totalCount,
      high,
      medium,
      low,
      avgConfidence: count > 0 ? Math.round((sum / count) * 100) : null,
    };
  }, [data]);

  const filteredItems = useMemo(() => {
    if (!data) return [];
    return data.items.filter((item) => matchesBand(item, filter));
  }, [data, filter]);

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 2 }}>
        Classification Queue
      </Typography>

      {stats && (
        <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', mb: 2 }}>
          <StatBox label="Total Pending" value={stats.totalPending} color="#00e5ff" />
          <StatBox label="High ≥70%" value={stats.high} color="#00e676" />
          <StatBox label="Medium 40–69%" value={stats.medium} color="#ffb300" />
          <StatBox label="Low <40%" value={stats.low} color="#ff1744" />
          {stats.avgConfidence != null && (
            <StatBox label="Avg Confidence" value={`${stats.avgConfidence}%`} color="#e6edf3" />
          )}
        </Box>
      )}

      {data && (
        <ToggleButtonGroup
          value={filter}
          exclusive
          onChange={(_, val) => { if (val !== null) setFilter(val); }}
          size="small"
          sx={{ mb: 2 }}
        >
          <ToggleButton value="all">ALL</ToggleButton>
          <ToggleButton value="high">HIGH ≥70%</ToggleButton>
          <ToggleButton value="medium">MEDIUM 40–69%</ToggleButton>
          <ToggleButton value="low">LOW &lt;40%</ToggleButton>
        </ToggleButtonGroup>
      )}

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
          <CircularProgress />
        </Box>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Failed to load classification queue: {error.message}
        </Alert>
      )}

      {data?.items.length === 0 && !isLoading && (
        <Typography color="text.secondary">No items in the classification queue.</Typography>
      )}

      {filteredItems.length === 0 && data && data.items.length > 0 && filter !== 'all' && (
        <Typography color="text.secondary" sx={{ py: 2 }}>
          No items match the selected confidence filter.
        </Typography>
      )}

      {filteredItems.map((item) => (
        <ClassificationQueueCard key={item.id} item={item} />
      ))}

      {data && data.totalCount > pageSize && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
          <Pagination
            count={Math.ceil(data.totalCount / pageSize)}
            page={page}
            onChange={(_, value) => setPage(value)}
            color="primary"
          />
        </Box>
      )}
    </Box>
  );
}
