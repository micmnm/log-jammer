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
import IconButton from '@mui/material/IconButton';
import Switch from '@mui/material/Switch';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import DialogContentText from '@mui/material/DialogContentText';
import TextField from '@mui/material/TextField';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import {
  useDataSources,
  useCreateDataSource,
  useUpdateDataSource,
  useDeleteDataSource,
} from '../api/hooks/useDataSources';
import type { DataSourceResponse, DataSourceType } from '../api/types';

function relativeTime(iso: string | null): string {
  if (!iso) return 'Never';
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60_000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}

interface DataSourceFormState {
  name: string;
  type: DataSourceType;
  connectionConfig: string;
  messageTemplate: string;
}

const DEFAULT_FORM: DataSourceFormState = {
  name: '',
  type: 'Elasticsearch',
  connectionConfig: '',
  messageTemplate: '',
};

interface DataSourceDialogProps {
  open: boolean;
  onClose: () => void;
  editing: DataSourceResponse | null;
}

function DataSourceDialog({ open, onClose, editing }: DataSourceDialogProps) {
  const [form, setForm] = useState<DataSourceFormState>(() =>
    editing
      ? {
          name: editing.name,
          type: editing.type,
          connectionConfig: editing.connectionConfig,
          messageTemplate: editing.messageTemplate ?? '',
        }
      : DEFAULT_FORM
  );

  const create = useCreateDataSource();
  const update = useUpdateDataSource();
  const isPending = create.isPending || update.isPending;
  const error = create.error ?? update.error;

  function handleChange(field: keyof DataSourceFormState, value: string) {
    setForm((prev) => ({ ...prev, [field]: value }));
  }

  function handleSubmit() {
    if (editing) {
      update.mutate(
        {
          id: editing.id,
          name: form.name,
          connectionConfig: form.connectionConfig,
          messageTemplate: form.messageTemplate || undefined,
        },
        { onSuccess: onClose }
      );
    } else {
      create.mutate(
        {
          name: form.name,
          type: form.type,
          connectionConfig: form.connectionConfig,
          messageTemplate: form.messageTemplate || undefined,
        },
        { onSuccess: onClose }
      );
    }
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{editing ? 'Edit Data Source' : 'Add Data Source'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2 }}>
        {error && (
          <Alert severity="error">
            {error instanceof Error ? error.message : 'An error occurred'}
          </Alert>
        )}
        <TextField
          label="Name"
          value={form.name}
          onChange={(e) => handleChange('name', e.target.value)}
          fullWidth
          required
          disabled={isPending}
        />
        <FormControl fullWidth disabled={!!editing || isPending}>
          <InputLabel>Type</InputLabel>
          <Select
            value={form.type}
            label="Type"
            onChange={(e) => handleChange('type', e.target.value as DataSourceType)}
          >
            <MenuItem value="Elasticsearch">Elasticsearch</MenuItem>
            <MenuItem value="KibanaProxy">KibanaProxy</MenuItem>
          </Select>
        </FormControl>
        {form.type === 'KibanaProxy' && (
          <Alert severity="info">Configured via Chrome extension</Alert>
        )}
        <TextField
          label="Connection Config"
          value={form.connectionConfig}
          onChange={(e) => handleChange('connectionConfig', e.target.value)}
          fullWidth
          multiline
          minRows={3}
          placeholder='{"url": "http://...", "index": "logs-*"}'
          disabled={isPending}
          slotProps={{
            input: {
              sx: { fontFamily: 'monospace', fontSize: '0.85rem' },
            },
          }}
        />
        <TextField
          label="Message Template (optional)"
          value={form.messageTemplate}
          onChange={(e) => handleChange('messageTemplate', e.target.value)}
          fullWidth
          placeholder="{message}"
          disabled={isPending}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={isPending}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={isPending || !form.name}
        >
          {isPending ? 'Saving…' : editing ? 'Save' : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

interface ConfirmDeleteDialogProps {
  open: boolean;
  name: string;
  onClose: () => void;
  onConfirm: () => void;
  isPending: boolean;
}

function ConfirmDeleteDialog({
  open,
  name,
  onClose,
  onConfirm,
  isPending,
}: ConfirmDeleteDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs">
      <DialogTitle>Delete Data Source</DialogTitle>
      <DialogContent>
        <DialogContentText>
          Are you sure you want to delete <strong>{name}</strong>? This cannot be undone.
        </DialogContentText>
      </DialogContent>
      <DialogActions sx={{ pb: 2, px: 3 }}>
        <Button onClick={onClose} disabled={isPending}>
          Cancel
        </Button>
        <Button color="error" variant="contained" onClick={onConfirm} disabled={isPending}>
          {isPending ? 'Deleting…' : 'Delete'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default function DataSources() {
  const { data: dataSources, isLoading } = useDataSources();
  const update = useUpdateDataSource();
  const deleteDs = useDeleteDataSource();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingDs, setEditingDs] = useState<DataSourceResponse | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DataSourceResponse | null>(null);

  function handleRowClick(ds: DataSourceResponse) {
    setEditingDs(ds);
    setDialogOpen(true);
  }

  function handleAddClick() {
    setEditingDs(null);
    setDialogOpen(true);
  }

  function handleDialogClose() {
    setDialogOpen(false);
    setEditingDs(null);
  }

  function handleToggleEnabled(ds: DataSourceResponse, e: React.ChangeEvent<HTMLInputElement>) {
    e.stopPropagation();
    update.mutate({ id: ds.id, enabled: e.target.checked });
  }

  function handleDeleteClick(ds: DataSourceResponse, e: React.MouseEvent) {
    e.stopPropagation();
    setDeleteTarget(ds);
  }

  function handleDeleteConfirm() {
    if (!deleteTarget) return;
    deleteDs.mutate(deleteTarget.id, {
      onSuccess: () => setDeleteTarget(null),
    });
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
        <Typography variant="h5" sx={{ flexGrow: 1, fontWeight: 600 }}>
          Data Sources
        </Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={handleAddClick}>
          Add Data Source
        </Button>
      </Box>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
          <CircularProgress />
        </Box>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Enabled</TableCell>
                <TableCell>Last Polled</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {!dataSources || dataSources.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No data sources configured
                  </TableCell>
                </TableRow>
              ) : (
                dataSources.map((ds) => (
                  <TableRow key={ds.id} onClick={() => handleRowClick(ds)}>
                    <TableCell sx={{ fontWeight: 500 }}>{ds.name}</TableCell>
                    <TableCell sx={{ color: 'text.secondary', fontSize: '0.8rem' }}>
                      {ds.type}
                    </TableCell>
                    <TableCell>
                      <Switch
                        checked={ds.enabled}
                        onChange={(e) => handleToggleEnabled(ds, e)}
                        onClick={(e) => e.stopPropagation()}
                        size="small"
                        color="primary"
                      />
                    </TableCell>
                    <TableCell sx={{ color: 'text.secondary', fontSize: '0.8rem' }}>
                      {relativeTime(ds.lastPolledAt)}
                    </TableCell>
                    <TableCell align="right">
                      <IconButton
                        size="small"
                        color="error"
                        onClick={(e) => handleDeleteClick(ds, e)}
                        aria-label="delete"
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {dialogOpen && (
        <DataSourceDialog
          open={dialogOpen}
          onClose={handleDialogClose}
          editing={editingDs}
        />
      )}

      {deleteTarget && (
        <ConfirmDeleteDialog
          open={!!deleteTarget}
          name={deleteTarget.name}
          onClose={() => setDeleteTarget(null)}
          onConfirm={handleDeleteConfirm}
          isPending={deleteDs.isPending}
        />
      )}
    </Box>
  );
}
