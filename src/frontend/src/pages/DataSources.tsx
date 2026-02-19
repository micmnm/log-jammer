import { useState } from 'react';
import {
  Box,
  Typography,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Switch,
  IconButton,
  Chip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Alert,
  Checkbox,
  FormControlLabel,
  CircularProgress,
  Divider,
  Tooltip,
} from '@mui/material';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import SchemaIcon from '@mui/icons-material/AccountTree';
import FingerprintIcon from '@mui/icons-material/Fingerprint';
import NetworkCheckIcon from '@mui/icons-material/NetworkCheck';
import AddIcon from '@mui/icons-material/Add';
import {
  useDataSources,
  useDeleteDataSource,
  useUpdateDataSource,
  useTestConnection,
  useDeletionImpact,
} from '../api/hooks/useDataSources';
import type { DataSourceResponse, ConnectionTestResponse } from '../api/types';
import DataSourceDialog from '../components/DataSourceDialog';
import SchemaMappingDialog from '../components/SchemaMappingDialog';
import FingerprintConfigDialog from '../components/FingerprintConfigDialog';

function formatRelativeTime(dateStr: string): string {
  const now = Date.now();
  const then = new Date(dateStr).getTime();
  const diff = now - then;
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days}d ago`;
  return `${Math.floor(days / 30)}mo ago`;
}

export default function DataSources() {
  const { data: dataSources, isLoading } = useDataSources();
  const deleteDataSource = useDeleteDataSource();
  const updateDataSource = useUpdateDataSource();
  const testConnection = useTestConnection();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingDs, setEditingDs] = useState<DataSourceResponse | null>(null);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);
  const [preserveHistory, setPreserveHistory] = useState(false);
  const [schemaDs, setSchemaDs] = useState<DataSourceResponse | null>(null);
  const [fingerprintDs, setFingerprintDs] = useState<DataSourceResponse | null>(null);
  const [testResult, setTestResult] = useState<{ id: string; result: ConnectionTestResponse } | null>(null);

  const { data: deletionImpact, isLoading: impactLoading } = useDeletionImpact(deleteConfirmId);

  const handleAdd = () => {
    setEditingDs(null);
    setDialogOpen(true);
  };

  const handleEdit = (ds: DataSourceResponse) => {
    setEditingDs(ds);
    setDialogOpen(true);
  };

  const handleOpenDeleteDialog = (id: string) => {
    setPreserveHistory(false);
    setDeleteConfirmId(id);
  };

  const handleDelete = () => {
    if (deleteConfirmId) {
      deleteDataSource.mutate({ id: deleteConfirmId, preserveHistory });
      setDeleteConfirmId(null);
    }
  };

  const handleToggleEnabled = (ds: DataSourceResponse) => {
    updateDataSource.mutate({ id: ds.id, request: { enabled: !ds.enabled } });
  };

  const handleTestConnection = (ds: DataSourceResponse) => {
    setTestResult(null);
    testConnection.mutate(ds.id, {
      onSuccess: (result) => setTestResult({ id: ds.id, result }),
    });
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">Data Sources</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={handleAdd}>
          Add Data Source
        </Button>
      </Box>

      {testResult && (
        <Alert
          severity={testResult.result.success ? 'success' : 'error'}
          onClose={() => setTestResult(null)}
          sx={{ mb: 2 }}
        >
          {testResult.result.success
            ? `Connection successful (${testResult.result.latencyMs.toFixed(0)}ms)`
            : `Connection failed: ${testResult.result.errorMessage}`}
        </Alert>
      )}

      {isLoading ? (
        <Typography>Loading...</Typography>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Adapter Type</TableCell>
                <TableCell>Enabled</TableCell>
                <TableCell>Poll Interval</TableCell>
                <TableCell>Sampling Budget</TableCell>
                <TableCell>Last Ingest</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dataSources?.map((ds) => (
                <TableRow key={ds.id}>
                  <TableCell>{ds.name}</TableCell>
                  <TableCell>
                    <Chip label={ds.adapterType} size="small" />
                  </TableCell>
                  <TableCell>
                    <Switch checked={ds.enabled} onChange={() => handleToggleEnabled(ds)} size="small" />
                  </TableCell>
                  <TableCell sx={{ fontFamily: (theme) => theme.fontFamilyMono }}>{ds.pollIntervalSeconds}s</TableCell>
                  <TableCell sx={{ fontFamily: (theme) => theme.fontFamilyMono }}>{ds.samplingBudget}</TableCell>
                  <TableCell>
                    {ds.lastIngestAt ? (
                      <Tooltip title={new Date(ds.lastIngestAt).toLocaleString()} arrow>
                        <Typography variant="body2" sx={{ cursor: 'default', fontFamily: (theme) => theme.fontFamilyMono, fontSize: '0.8rem' }}>
                          {formatRelativeTime(ds.lastIngestAt)}
                        </Typography>
                      </Tooltip>
                    ) : (
                      <Typography variant="body2" color="text.secondary" sx={{ fontSize: '0.8rem' }}>
                        Never
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => handleEdit(ds)} title="Edit">
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => setSchemaDs(ds)} title="Schema Mapping">
                      <SchemaIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => setFingerprintDs(ds)} title="Fingerprint Config">
                      <FingerprintIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      onClick={() => handleTestConnection(ds)}
                      disabled={testConnection.isPending}
                      title="Test Connection"
                    >
                      <NetworkCheckIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => handleOpenDeleteDialog(ds.id)} title="Delete">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
              {dataSources?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} align="center">
                    <Typography variant="body2" color="text.secondary">
                      No data sources configured.
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <DataSourceDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        dataSource={editingDs}
      />

      {schemaDs && (
        <SchemaMappingDialog
          open={!!schemaDs}
          onClose={() => setSchemaDs(null)}
          dataSource={schemaDs}
        />
      )}

      {fingerprintDs && (
        <FingerprintConfigDialog
          open={!!fingerprintDs}
          onClose={() => setFingerprintDs(null)}
          dataSource={fingerprintDs}
        />
      )}

      <Dialog open={!!deleteConfirmId} onClose={() => setDeleteConfirmId(null)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <WarningAmberIcon color="warning" />
          Delete Data Source
        </DialogTitle>
        <DialogContent>
          {impactLoading ? (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
              <CircularProgress size={32} />
            </Box>
          ) : deletionImpact ? (
            <Box>
              <Alert severity="warning" sx={{ mb: 2 }}>
                This will permanently delete the data source and all associated data.
              </Alert>

              <Typography variant="subtitle2" sx={{ mb: 1 }}>
                Cascade impact:
              </Typography>
              <Box
                component="ul"
                sx={{
                  pl: 2,
                  mb: 2,
                  fontFamily: (theme) => theme.fontFamilyMono,
                  '& li': { py: 0.25 },
                }}
              >
                {deletionImpact.errorGroupCount > 0 && (
                  <li>
                    <Typography variant="body2" component="span" color="error.main">
                      {deletionImpact.errorGroupCount}
                    </Typography>{' '}
                    error group{deletionImpact.errorGroupCount !== 1 ? 's' : ''}
                  </li>
                )}
                {deletionImpact.occurrenceCount > 0 && (
                  <li>
                    <Typography variant="body2" component="span" color="error.main">
                      {deletionImpact.occurrenceCount}
                    </Typography>{' '}
                    occurrence{deletionImpact.occurrenceCount !== 1 ? 's' : ''}
                  </li>
                )}
                {deletionImpact.alertCount > 0 && (
                  <li>
                    <Typography variant="body2" component="span" color="error.main">
                      {deletionImpact.alertCount}
                    </Typography>{' '}
                    alert{deletionImpact.alertCount !== 1 ? 's' : ''}
                  </li>
                )}
                {deletionImpact.classificationQueueCount > 0 && (
                  <li>
                    <Typography variant="body2" component="span" color="error.main">
                      {deletionImpact.classificationQueueCount}
                    </Typography>{' '}
                    classification queue item{deletionImpact.classificationQueueCount !== 1 ? 's' : ''}
                  </li>
                )}
                {deletionImpact.tagCount > 0 && (
                  <li>
                    <Typography variant="body2" component="span" color="error.main">
                      {deletionImpact.tagCount}
                    </Typography>{' '}
                    tag assignment{deletionImpact.tagCount !== 1 ? 's' : ''}
                  </li>
                )}
                {deletionImpact.ruleCount > 0 && (
                  <li>
                    <Typography variant="body2" component="span" color="error.main">
                      {deletionImpact.ruleCount}
                    </Typography>{' '}
                    spike detection rule{deletionImpact.ruleCount !== 1 ? 's' : ''}
                  </li>
                )}
                {deletionImpact.errorGroupCount === 0 &&
                  deletionImpact.occurrenceCount === 0 &&
                  deletionImpact.alertCount === 0 && (
                    <li>
                      <Typography variant="body2" color="text.secondary">
                        No associated data found.
                      </Typography>
                    </li>
                  )}
              </Box>

              {deletionImpact.errorGroupCount > 0 && (
                <>
                  <Divider sx={{ my: 1.5 }} />
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={preserveHistory}
                        onChange={(e) => setPreserveHistory(e.target.checked)}
                      />
                    }
                    label="Keep historical error groups for future classification"
                  />
                  {preserveHistory && (
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', ml: 4 }}>
                      Error groups and their occurrences, alerts, and tags will be preserved but
                      detached from this data source.
                    </Typography>
                  )}
                </>
              )}
            </Box>
          ) : (
            <Typography color="text.secondary">
              Are you sure you want to delete this data source?
            </Typography>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteConfirmId(null)}>Cancel</Button>
          <Button
            onClick={handleDelete}
            color="error"
            variant="contained"
            disabled={impactLoading}
          >
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
