import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Grid from '@mui/material/Grid';
import Button from '@mui/material/Button';
import Snackbar from '@mui/material/Snackbar';
import { useTheme } from '@mui/material/styles';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  Filler,
} from 'chart.js';
import { Line } from 'react-chartjs-2';
import { usePatternDetail, useAcknowledgePattern } from '../api/hooks/usePatterns';
import SeverityChip from '../components/SeverityChip';

ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  Filler
);

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString();
}

function relativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60_000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}

export default function PatternDetail() {
  const { id } = useParams<{ id: string }>();
  const { data: pattern, isLoading, error } = usePatternDetail(id ?? '');
  const theme = useTheme();
  const navigate = useNavigate();
  const acknowledge = useAcknowledgePattern();
  const [similarMessage, setSimilarMessage] = useState('');

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        void navigate(-1);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [navigate]);

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !pattern) {
    return (
      <Alert severity="error">
        {error instanceof Error ? error.message : 'Pattern not found'}
      </Alert>
    );
  }

  const chartLabels = pattern.occurrences.map((o) =>
    new Date(o.windowStart).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  );

  // Map baseline bands by hourOfWeek for O(1) lookup
  const baselineByHour = new Map(
    pattern.baselineBands.map((b) => [b.hourOfWeek, b])
  );

  // Align baseline data to occurrence timestamps by computing hourOfWeek for each window
  const alignedUpper = pattern.occurrences.map((o) => {
    const d = new Date(o.windowStart);
    const hourOfWeek = d.getUTCDay() * 24 + d.getUTCHours();
    const b = baselineByHour.get(hourOfWeek);
    return b ? b.avgCount + b.stdDevCount : null;
  });

  const alignedLower = pattern.occurrences.map((o) => {
    const d = new Date(o.windowStart);
    const hourOfWeek = d.getUTCDay() * 24 + d.getUTCHours();
    const b = baselineByHour.get(hourOfWeek);
    return b ? Math.max(0, b.avgCount - b.stdDevCount) : null;
  });

  const hasBaseline = alignedUpper.some((v) => v !== null);

  const primaryColor = theme.palette.primary.main;
  const secondaryColor = theme.palette.secondary.main;
  const textSecondary = theme.palette.text.secondary;
  const gridColor = theme.palette.divider;

  const chartData = {
    labels: chartLabels,
    datasets: [
      {
        label: 'Count',
        data: pattern.occurrences.map((o) => o.count),
        borderColor: primaryColor,
        backgroundColor: `${primaryColor}1a`,
        borderWidth: 2,
        pointRadius: 2,
        tension: 0.3,
        fill: false,
      },
      ...(hasBaseline
        ? [
            {
              label: 'Expected (upper)',
              data: alignedUpper,
              borderColor: `${secondaryColor}66`,
              backgroundColor: `${secondaryColor}1a`,
              borderWidth: 1,
              borderDash: [4, 4],
              pointRadius: 0,
              fill: '+1',
              tension: 0.3,
            },
            {
              label: 'Expected (lower)',
              data: alignedLower,
              borderColor: `${secondaryColor}66`,
              backgroundColor: `${secondaryColor}1a`,
              borderWidth: 1,
              borderDash: [4, 4],
              pointRadius: 0,
              fill: false,
              tension: 0.3,
            },
          ]
        : []),
    ],
  };

  const chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        labels: {
          color: textSecondary,
          font: { size: 11 },
        },
      },
      title: { display: false },
    },
    scales: {
      x: {
        ticks: { color: textSecondary, font: { size: 10 } },
        grid: { color: gridColor },
      },
      y: {
        ticks: { color: textSecondary, font: { size: 10 } },
        grid: { color: gridColor },
        beginAtZero: true,
      },
    },
  };

  return (
    <Box>
      {/* Header with Acknowledge */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Button size="small" onClick={() => void navigate(-1)}>
          ← Back
        </Button>
        {pattern.isNew && (
          <Button
            variant="contained"
            size="small"
            onClick={() => {
              acknowledge.mutate(pattern.id, {
                onSuccess: (result) => {
                  if (result && result.similarCount > 0) {
                    setSimilarMessage(
                      `Also acknowledged ${result.similarCount} similar pattern${result.similarCount > 1 ? 's' : ''}`
                    );
                  }
                },
              });
            }}
            disabled={acknowledge.isPending}
          >
            Acknowledge
          </Button>
        )}
      </Box>

      {/* Template */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="overline" color="text.secondary">
          Log Pattern Template
        </Typography>
        <Typography
          variant="body1"
          sx={{
            mt: 1,
            fontFamily: 'monospace',
            fontSize: '0.9rem',
            color: 'primary.main',
            wordBreak: 'break-all',
            whiteSpace: 'pre-wrap',
          }}
        >
          {pattern.template}
        </Typography>
        {pattern.sampleMessage && (
          <>
            <Typography variant="overline" color="text.secondary" sx={{ mt: 2, display: 'block' }}>
              Sample Message
            </Typography>
            <Typography
              variant="body2"
              sx={{
                mt: 0.5,
                fontFamily: 'monospace',
                color: 'text.secondary',
                wordBreak: 'break-all',
                whiteSpace: 'pre-wrap',
              }}
            >
              {pattern.sampleMessage}
            </Typography>
          </>
        )}
      </Paper>

      {/* Meta */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="overline" color="text.secondary">
              Severity
            </Typography>
            <Box sx={{ mt: 1 }}>
              <SeverityChip severity={pattern.severity} />
            </Box>
          </Paper>
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="overline" color="text.secondary">
              Data Source
            </Typography>
            <Typography variant="body2" sx={{ mt: 1, fontWeight: 500 }}>
              {pattern.dataSourceName}
            </Typography>
          </Paper>
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="overline" color="text.secondary">
              First Seen
            </Typography>
            <Typography variant="body2" sx={{ mt: 1 }}>
              {formatDate(pattern.firstSeen)}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {relativeTime(pattern.firstSeen)}
            </Typography>
          </Paper>
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="overline" color="text.secondary">
              Last Seen
            </Typography>
            <Typography variant="body2" sx={{ mt: 1 }}>
              {formatDate(pattern.lastSeen)}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {relativeTime(pattern.lastSeen)}
            </Typography>
          </Paper>
        </Grid>
      </Grid>

      {/* Rate */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="overline" color="text.secondary">
              Current Rate
            </Typography>
            <Typography variant="h4" sx={{ mt: 1, fontFamily: 'monospace', color: 'primary.main' }}>
              {pattern.currentRate.toFixed(2)}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              events / hour
            </Typography>
          </Paper>
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="overline" color="text.secondary">
              Expected Rate
            </Typography>
            <Typography variant="h4" sx={{ mt: 1, fontFamily: 'monospace' }}>
              {pattern.expectedRate.toFixed(2)}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {pattern.stdDevsFromMean > 0
                ? `${pattern.stdDevsFromMean.toFixed(1)}σ above baseline`
                : `${Math.abs(pattern.stdDevsFromMean).toFixed(1)}σ below baseline`}
            </Typography>
          </Paper>
        </Grid>
      </Grid>

      {/* Chart */}
      <Paper sx={{ p: 3 }}>
        <Typography variant="subtitle1" sx={{ mb: 2, fontWeight: 600 }}>
          Occurrence History
        </Typography>
        {pattern.occurrences.length === 0 ? (
          <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>
            No occurrence data available
          </Typography>
        ) : (
          <Box sx={{ height: 300 }}>
            <Line data={chartData} options={chartOptions} />
          </Box>
        )}
      </Paper>

      <Snackbar
        open={!!similarMessage}
        autoHideDuration={4000}
        onClose={() => setSimilarMessage('')}
        message={similarMessage}
      />
    </Box>
  );
}
