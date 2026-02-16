import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Box,
  Typography,
  Button,
  Stack,
  Card,
  CardContent,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  CircularProgress,
  Alert,
  TextField,
} from '@mui/material';
import { useTheme } from '@mui/material/styles';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
} from 'chart.js';
import { Line } from 'react-chartjs-2';
import {
  useErrorGroup,
  useErrorGroupOccurrences,
  useUpdateErrorGroupStatus,
  useUpdateErrorGroupSeverity,
} from '../api/hooks/useErrorGroups';
import { useAlerts } from '../api/hooks/useAlerts';
import SeverityChip from '../components/SeverityChip';
import StatusChip from '../components/StatusChip';
import AlertCard from '../components/AlertCard';
import type { ErrorSeverity, ErrorStatus } from '../api/types';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend);

export default function ErrorGroupDetail() {
  const theme = useTheme();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');

  const { data: errorGroup, isLoading, error } = useErrorGroup(id!);
  const { data: occurrences } = useErrorGroupOccurrences(
    id!,
    fromDate || undefined,
    toDate || undefined,
  );
  const { data: alertsData } = useAlerts();
  const updateStatus = useUpdateErrorGroupStatus();
  const updateSeverity = useUpdateErrorGroupSeverity();

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !errorGroup) {
    return <Alert severity="error">Failed to load error group: {error?.message ?? 'Not found'}</Alert>;
  }

  const relatedAlerts = alertsData?.items.filter((a) => a.knownErrorId === id) ?? [];

  const hasSampling = occurrences?.some((o) => o.sampleRatio < 1.0) ?? false;

  const chartData = {
    labels: occurrences?.map((o) => new Date(o.windowStart).toLocaleString()) ?? [],
    datasets: [
      {
        label: 'Extrapolated Count',
        data: occurrences?.map((o) => o.extrapolatedCount) ?? [],
        borderColor: '#00e5ff',
        backgroundColor: 'rgba(0, 229, 255, 0.1)',
        fill: true,
        tension: 0.3,
      },
    ],
  };

  return (
    <Box>
      <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/error-groups')} sx={{ mb: 2 }}>
        Back to Error Groups
      </Button>

      <Stack direction="row" alignItems="center" spacing={2} sx={{ mb: 2 }}>
        <Typography variant="h5" sx={{ flex: 1, wordBreak: 'break-word' }}>
          {errorGroup.representativeMessage}
        </Typography>
        <SeverityChip severity={errorGroup.severity} />
        <StatusChip status={errorGroup.status} />
      </Stack>

      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Stack direction="row" spacing={4} flexWrap="wrap">
            <Box>
              <Typography variant="caption" color="text.secondary">Data Source</Typography>
              <Typography variant="body2">{errorGroup.dataSourceName ?? 'Unknown'}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">First Seen</Typography>
              <Typography variant="body2">{new Date(errorGroup.firstSeen).toLocaleString()}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">Last Seen</Typography>
              <Typography variant="body2">{new Date(errorGroup.lastSeen).toLocaleString()}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">Total Occurrences</Typography>
              <Typography
                variant="body2"
                sx={{ fontFamily: theme.fontFamilyMono, fontWeight: 500 }}
              >
                {errorGroup.totalOccurrences}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">Fingerprint</Typography>
              <Typography
                variant="body2"
                sx={{ fontFamily: theme.fontFamilyMono, fontSize: '0.75rem' }}
              >
                {errorGroup.fingerprintHash}
              </Typography>
            </Box>
          </Stack>
        </CardContent>
      </Card>

      <Stack direction="row" spacing={2} sx={{ mb: 3 }}>
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel>Severity</InputLabel>
          <Select
            value={errorGroup.severity}
            label="Severity"
            onChange={(e) =>
              updateSeverity.mutate({ id: id!, severity: e.target.value as ErrorSeverity })
            }
          >
            <MenuItem value="Critical">Critical</MenuItem>
            <MenuItem value="Warning">Warning</MenuItem>
            <MenuItem value="Info">Info</MenuItem>
          </Select>
        </FormControl>

        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel>Status</InputLabel>
          <Select
            value={errorGroup.status}
            label="Status"
            onChange={(e) =>
              updateStatus.mutate({ id: id!, status: e.target.value as ErrorStatus })
            }
          >
            <MenuItem value="Active">Active</MenuItem>
            <MenuItem value="Resolved">Resolved</MenuItem>
            <MenuItem value="Ignored">Ignored</MenuItem>
            <MenuItem value="Expected">Expected</MenuItem>
          </Select>
        </FormControl>
      </Stack>

      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" sx={{ mb: 2 }}>Occurrences</Typography>
          <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
            <TextField
              label="From"
              type="datetime-local"
              size="small"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="To"
              type="datetime-local"
              size="small"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Stack>
          {hasSampling && (
            <Alert severity="info" sx={{ mb: 2 }}>
              Some data points use sampling (sampleRatio &lt; 1.0). Counts are extrapolated.
            </Alert>
          )}
          <Box sx={{ height: 300 }}>
            <Line
              data={chartData}
              options={{
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                  x: {
                    ticks: { autoSkip: true, maxRotation: 45, color: '#8b949e' },
                    grid: { color: 'rgba(0, 229, 255, 0.06)' },
                  },
                  y: {
                    beginAtZero: true,
                    ticks: { color: '#8b949e' },
                    grid: { color: 'rgba(0, 229, 255, 0.06)' },
                  },
                },
              }}
            />
          </Box>
        </CardContent>
      </Card>

      {errorGroup.representativeStackTrace && (
        <Accordion variant="outlined" sx={{ mb: 3 }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography variant="h6">Stack Trace</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Box
              component="pre"
              sx={{
                fontFamily: theme.fontFamilyMono,
                fontSize: '0.75rem',
                overflow: 'auto',
                maxHeight: 400,
                m: 0,
              }}
            >
              {errorGroup.representativeStackTrace}
            </Box>
          </AccordionDetails>
        </Accordion>
      )}

      {relatedAlerts.length > 0 && (
        <Box>
          <Typography variant="h6" sx={{ mb: 2 }}>Alert History</Typography>
          {relatedAlerts.map((alert) => (
            <AlertCard key={alert.id} alert={alert} showAcknowledge={false} />
          ))}
        </Box>
      )}
    </Box>
  );
}
