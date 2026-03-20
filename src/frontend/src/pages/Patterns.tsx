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
import TablePagination from '@mui/material/TablePagination';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import InputAdornment from '@mui/material/InputAdornment';
import SearchIcon from '@mui/icons-material/Search';
import { useNavigate } from 'react-router-dom';
import { usePatterns } from '../api/hooks/usePatterns';
import type { PatternFilters } from '../api/hooks/usePatterns';
import type { Severity } from '../api/types';
import SeverityChip from '../components/SeverityChip';

function relativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60_000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}

function truncate(str: string, max = 100): string {
  return str.length > max ? `${str.slice(0, max)}...` : str;
}

export default function Patterns() {
  const navigate = useNavigate();
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);
  const [search, setSearch] = useState('');
  const [severityFilter, setSeverityFilter] = useState<Severity | ''>('');
  const [statusFilter, setStatusFilter] = useState<string>('');

  const filters: PatternFilters = {
    page: page + 1,
    pageSize: rowsPerPage,
  };
  if (search) filters.search = search;
  if (severityFilter) filters.severity = severityFilter;
  if (statusFilter === 'new') filters.isNew = true;
  if (statusFilter === 'known') filters.isNew = false;

  const { data, isLoading } = usePatterns(filters);

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 3, fontWeight: 600 }}>
        Log Patterns
      </Typography>

      {/* Filters */}
      <Box sx={{ display: 'flex', gap: 2, mb: 3 }}>
        <TextField
          size="small"
          placeholder="Search patterns..."
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(0); }}
          sx={{ minWidth: 300 }}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" />
                </InputAdornment>
              ),
            },
          }}
        />
        <TextField
          size="small"
          select
          label="Severity"
          value={severityFilter}
          onChange={(e) => { setSeverityFilter(e.target.value as Severity | ''); setPage(0); }}
          sx={{ minWidth: 130 }}
        >
          <MenuItem value="">All</MenuItem>
          <MenuItem value="Critical">Critical</MenuItem>
          <MenuItem value="Error">Error</MenuItem>
          <MenuItem value="Warning">Warning</MenuItem>
          <MenuItem value="Info">Info</MenuItem>
        </TextField>
        <TextField
          size="small"
          select
          label="Status"
          value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value); setPage(0); }}
          sx={{ minWidth: 130 }}
        >
          <MenuItem value="">All</MenuItem>
          <MenuItem value="new">New</MenuItem>
          <MenuItem value="known">Known</MenuItem>
        </TextField>
      </Box>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          <TableContainer component={Paper}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Template</TableCell>
                  <TableCell>Severity</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell align="right">Rate / hr</TableCell>
                  <TableCell align="right">Expected</TableCell>
                  <TableCell align="right">Deviation</TableCell>
                  <TableCell>Data Source</TableCell>
                  <TableCell>First Seen</TableCell>
                  <TableCell>Last Seen</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {(!data || data.items.length === 0) ? (
                  <TableRow>
                    <TableCell colSpan={9} align="center" sx={{ py: 6, color: 'text.secondary' }}>
                      No patterns found
                    </TableCell>
                  </TableRow>
                ) : (
                  data.items.map((p) => (
                    <TableRow
                      key={p.id}
                      hover
                      onClick={() => void navigate(`/patterns/${p.id}`)}
                      sx={{ cursor: 'pointer' }}
                    >
                      <TableCell
                        sx={{
                          fontFamily: 'monospace',
                          fontSize: '0.8rem',
                          maxWidth: 400,
                          whiteSpace: 'nowrap',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                        }}
                        title={p.template}
                      >
                        {truncate(p.template)}
                      </TableCell>
                      <TableCell>
                        <SeverityChip severity={p.severity} />
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={p.isNew ? 'New' : 'Known'}
                          size="small"
                          color={p.isNew ? 'warning' : 'default'}
                          variant={p.isNew ? 'filled' : 'outlined'}
                        />
                      </TableCell>
                      <TableCell align="right" sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>
                        {p.currentRate.toFixed(1)}
                      </TableCell>
                      <TableCell align="right" sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>
                        {p.expectedRate.toFixed(1)}
                      </TableCell>
                      <TableCell align="right">
                        {Math.abs(p.stdDevsFromMean) >= 0.1 ? (
                          <Chip
                            label={`${p.stdDevsFromMean > 0 ? '+' : ''}${p.stdDevsFromMean.toFixed(1)}σ`}
                            size="small"
                            color={Math.abs(p.stdDevsFromMean) >= 3 ? 'error' : Math.abs(p.stdDevsFromMean) >= 1 ? 'warning' : 'default'}
                            variant="outlined"
                          />
                        ) : (
                          <Typography variant="caption" color="text.secondary">--</Typography>
                        )}
                      </TableCell>
                      <TableCell sx={{ color: 'text.secondary', fontSize: '0.8rem', whiteSpace: 'nowrap' }}>
                        {p.dataSourceName}
                      </TableCell>
                      <TableCell sx={{ color: 'text.secondary', fontSize: '0.8rem', whiteSpace: 'nowrap' }}>
                        {relativeTime(p.firstSeen)}
                      </TableCell>
                      <TableCell sx={{ color: 'text.secondary', fontSize: '0.8rem', whiteSpace: 'nowrap' }}>
                        {relativeTime(p.lastSeen)}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </TableContainer>
          {data && (
            <TablePagination
              component="div"
              count={data.totalCount}
              page={page}
              onPageChange={(_, p) => setPage(p)}
              rowsPerPage={rowsPerPage}
              onRowsPerPageChange={(e) => {
                setRowsPerPage(parseInt(e.target.value, 10));
                setPage(0);
              }}
              rowsPerPageOptions={[10, 25, 50, 100]}
            />
          )}
        </>
      )}
    </Box>
  );
}
