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
  DialogContentText,
  DialogActions,
  Alert,
} from '@mui/material';
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
} from '../api/hooks/useDataSources';
import type { DataSourceResponse, ConnectionTestResponse } from '../api/types';
import DataSourceDialog from '../components/DataSourceDialog';
import SchemaMappingDialog from '../components/SchemaMappingDialog';
import FingerprintConfigDialog from '../components/FingerprintConfigDialog';

export default function DataSources() {
  const { data: dataSources, isLoading } = useDataSources();
  const deleteDataSource = useDeleteDataSource();
  const updateDataSource = useUpdateDataSource();
  const testConnection = useTestConnection();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingDs, setEditingDs] = useState<DataSourceResponse | null>(null);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);
  const [schemaDs, setSchemaDs] = useState<DataSourceResponse | null>(null);
  const [fingerprintDs, setFingerprintDs] = useState<DataSourceResponse | null>(null);
  const [testResult, setTestResult] = useState<{ id: string; result: ConnectionTestResponse } | null>(null);

  const handleAdd = () => {
    setEditingDs(null);
    setDialogOpen(true);
  };

  const handleEdit = (ds: DataSourceResponse) => {
    setEditingDs(ds);
    setDialogOpen(true);
  };

  const handleDelete = () => {
    if (deleteConfirmId) {
      deleteDataSource.mutate(deleteConfirmId);
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
                  <TableCell>{ds.pollIntervalSeconds}s</TableCell>
                  <TableCell>{ds.samplingBudget}</TableCell>
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
                    <IconButton size="small" onClick={() => setDeleteConfirmId(ds.id)} title="Delete">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
              {dataSources?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} align="center">
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

      <Dialog open={!!deleteConfirmId} onClose={() => setDeleteConfirmId(null)}>
        <DialogTitle>Delete Data Source</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to delete this data source? This action cannot be undone.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteConfirmId(null)}>Cancel</Button>
          <Button onClick={handleDelete} color="error" variant="contained">
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
