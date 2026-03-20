import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import { useDashboard } from '../api/hooks/useDashboard';
import { useAcknowledgePattern, useAcknowledgeAll } from '../api/hooks/usePatterns';
import SeverityChip from '../components/SeverityChip';
import { useNavigate } from 'react-router-dom';
import type { AnomalyItem } from '../api/types';

function relativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60_000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}

function truncate(str: string, max = 80): string {
  return str.length > max ? `${str.slice(0, max)}…` : str;
}

function deviationSortKey(a: AnomalyItem): number {
  return Math.abs(a.stdDevsFromMean);
}

export default function Dashboard() {
  const { data, isLoading } = useDashboard();
  const acknowledge = useAcknowledgePattern();
  const acknowledgeAll = useAcknowledgeAll();
  const navigate = useNavigate();
  const [similarMessage, setSimilarMessage] = useState<string | null>(null);

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  const dashboard = data ?? {
    totalPatterns: 0,
    newPatternCount: 0,
    ingestionRatePerHour: 0,
    topAnomalies: [],
    newPatterns: [],
  };

  const sortedAnomalies = [...dashboard.topAnomalies].sort(
    (a, b) => deviationSortKey(b) - deviationSortKey(a)
  );

  return (
    <Box>
      {/* Stats bar */}
      <Box sx={{ display: 'flex', gap: 2, mb: 4 }}>
        <Paper sx={{ flex: 1, p: 3, textAlign: 'center' }}>
          <Typography variant="h3" sx={{ color: 'primary.main', fontWeight: 700 }}>
            {dashboard.totalPatterns}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Total Patterns
          </Typography>
        </Paper>
        <Paper sx={{ flex: 1, p: 3, textAlign: 'center' }}>
          <Typography variant="h3" sx={{ color: 'warning.main', fontWeight: 700 }}>
            {dashboard.newPatternCount}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            New Patterns
          </Typography>
        </Paper>
        <Paper sx={{ flex: 1, p: 3, textAlign: 'center' }}>
          <Typography variant="h3" sx={{ color: 'text.primary', fontWeight: 700 }}>
            {dashboard.ingestionRatePerHour.toLocaleString()}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Ingestion Rate / hr
          </Typography>
        </Paper>
      </Box>

      {/* New Patterns */}
      <Box sx={{ mb: 4 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', mb: 2, gap: 2 }}>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            New Patterns
          </Typography>
          {dashboard.newPatterns.length > 0 && (
            <Button
              variant="outlined"
              size="small"
              onClick={() => acknowledgeAll.mutate(undefined)}
              disabled={acknowledgeAll.isPending}
            >
              Acknowledge All
            </Button>
          )}
        </Box>
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Template</TableCell>
                <TableCell>Severity</TableCell>
                <TableCell>First Seen</TableCell>
                <TableCell>Data Source</TableCell>
                <TableCell align="right">Action</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dashboard.newPatterns.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No new patterns
                  </TableCell>
                </TableRow>
              ) : (
                dashboard.newPatterns.map((p) => (
                  <TableRow
                    key={p.patternId}
                    onClick={() => void navigate(`/patterns/${p.patternId}`)}
                    sx={{ cursor: 'pointer' }}
                  >
                    <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>
                      {truncate(p.template)}
                    </TableCell>
                    <TableCell>
                      <SeverityChip severity={p.severity} />
                    </TableCell>
                    <TableCell sx={{ whiteSpace: 'nowrap', color: 'text.secondary', fontSize: '0.8rem' }}>
                      {relativeTime(p.firstSeen)}
                    </TableCell>
                    <TableCell sx={{ color: 'text.secondary', fontSize: '0.8rem' }}>
                      {p.dataSourceName}
                    </TableCell>
                    <TableCell align="right">
                      <Button
                        size="small"
                        variant="text"
                        onClick={(e) => {
                          e.stopPropagation();
                          acknowledge.mutate(p.patternId, {
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
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>

      {/* Anomalies */}
      <Box>
        <Typography variant="h6" sx={{ mb: 2 }}>
          Anomalies
        </Typography>
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Template</TableCell>
                <TableCell>Severity</TableCell>
                <TableCell align="right">Current Rate</TableCell>
                <TableCell align="right">Expected Rate</TableCell>
                <TableCell align="right">Deviation</TableCell>
                <TableCell>Data Source</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {sortedAnomalies.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No anomalies detected
                  </TableCell>
                </TableRow>
              ) : (
                sortedAnomalies.map((a) => (
                  <TableRow
                    key={a.patternId}
                    onClick={() => void navigate(`/patterns/${a.patternId}`)}
                    sx={{ cursor: 'pointer' }}
                  >
                    <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>
                      {truncate(a.template)}
                    </TableCell>
                    <TableCell>
                      <SeverityChip severity={a.severity} />
                    </TableCell>
                    <TableCell align="right" sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>
                      {a.currentRate.toFixed(1)}
                    </TableCell>
                    <TableCell align="right" sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>
                      {a.expectedRate.toFixed(1)}
                    </TableCell>
                    <TableCell align="right">
                      <Chip
                        label={`${Math.abs(a.stdDevsFromMean).toFixed(1)}σ`}
                        size="small"
                        color={Math.abs(a.stdDevsFromMean) >= 3 ? 'error' : 'warning'}
                        variant="outlined"
                      />
                    </TableCell>
                    <TableCell sx={{ color: 'text.secondary', fontSize: '0.8rem' }}>
                      {a.dataSourceName}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>
      <Snackbar
        open={!!similarMessage}
        autoHideDuration={5000}
        onClose={() => setSimilarMessage(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert onClose={() => setSimilarMessage(null)} severity="info" variant="filled">
          {similarMessage}
        </Alert>
      </Snackbar>
    </Box>
  );
}
