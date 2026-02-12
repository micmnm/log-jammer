import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Checkbox,
  Switch,
  IconButton,
  Typography,
  Box,
  CircularProgress,
} from '@mui/material';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import type { DataSourceResponse } from '../api/types';
import { useDataSourceSchema } from '../api/hooks/useDataSources';
import {
  useFingerprintConfigs,
  useCreateFingerprintConfig,
  useDeleteFingerprintConfig,
} from '../api/hooks/useFingerprintConfigs';

interface FieldEntry {
  fieldName: string;
  selected: boolean;
  order: number;
  normalizeBeforeHash: boolean;
}

interface Props {
  open: boolean;
  onClose: () => void;
  dataSource: DataSourceResponse;
}

export default function FingerprintConfigDialog({ open, onClose, dataSource }: Props) {
  const { data: schema, isLoading: schemaLoading } = useDataSourceSchema(dataSource.id);
  const { data: existingConfigs } = useFingerprintConfigs(dataSource.id);
  const createConfig = useCreateFingerprintConfig(dataSource.id);
  const deleteConfig = useDeleteFingerprintConfig(dataSource.id);

  const [fields, setFields] = useState<FieldEntry[]>([]);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open || !schema) return;

    const configMap = new Map(
      (existingConfigs ?? []).map((c) => [c.fieldName, c]),
    );

    const entries: FieldEntry[] = schema.fields.map((f) => {
      const existing = configMap.get(f.name);
      return {
        fieldName: f.name,
        selected: !!existing,
        order: existing?.order ?? 0,
        normalizeBeforeHash: existing?.normalizeBeforeHash ?? true,
      };
    });

    // Sort: selected fields first by order, then unselected alphabetically
    entries.sort((a, b) => {
      if (a.selected && !b.selected) return -1;
      if (!a.selected && b.selected) return 1;
      if (a.selected && b.selected) return a.order - b.order;
      return a.fieldName.localeCompare(b.fieldName);
    });

    setFields(entries);
  }, [open, schema, existingConfigs]);

  const toggleField = (fieldName: string) => {
    setFields((prev) =>
      prev.map((f) =>
        f.fieldName === fieldName ? { ...f, selected: !f.selected } : f,
      ),
    );
  };

  const toggleNormalize = (fieldName: string) => {
    setFields((prev) =>
      prev.map((f) =>
        f.fieldName === fieldName ? { ...f, normalizeBeforeHash: !f.normalizeBeforeHash } : f,
      ),
    );
  };

  const moveField = (fieldName: string, direction: 'up' | 'down') => {
    setFields((prev) => {
      const selected = prev.filter((f) => f.selected);
      const unselected = prev.filter((f) => !f.selected);
      const idx = selected.findIndex((f) => f.fieldName === fieldName);
      if (idx < 0) return prev;
      const swapIdx = direction === 'up' ? idx - 1 : idx + 1;
      if (swapIdx < 0 || swapIdx >= selected.length) return prev;
      const next = [...selected];
      [next[idx], next[swapIdx]] = [next[swapIdx], next[idx]];
      return [...next, ...unselected];
    });
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      // Delete all existing configs
      if (existingConfigs) {
        for (const config of existingConfigs) {
          await deleteConfig.mutateAsync(config.id);
        }
      }

      // Create new configs for selected fields
      const selected = fields.filter((f) => f.selected);
      for (let i = 0; i < selected.length; i++) {
        await createConfig.mutateAsync({
          fieldName: selected[i].fieldName,
          order: i,
          normalizeBeforeHash: selected[i].normalizeBeforeHash,
        });
      }

      onClose();
    } finally {
      setSaving(false);
    }
  };

  const selectedFields = fields.filter((f) => f.selected);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Fingerprint Config - {dataSource.name}</DialogTitle>
      <DialogContent>
        {schemaLoading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress />
          </Box>
        ) : (
          <>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Select fields to use for fingerprinting. Order determines hash priority.
            </Typography>
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell padding="checkbox" />
                    <TableCell>Field</TableCell>
                    <TableCell>Normalize</TableCell>
                    <TableCell>Order</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {fields.map((field) => (
                    <TableRow key={field.fieldName}>
                      <TableCell padding="checkbox">
                        <Checkbox
                          checked={field.selected}
                          onChange={() => toggleField(field.fieldName)}
                        />
                      </TableCell>
                      <TableCell>{field.fieldName}</TableCell>
                      <TableCell>
                        <Switch
                          size="small"
                          checked={field.normalizeBeforeHash}
                          onChange={() => toggleNormalize(field.fieldName)}
                          disabled={!field.selected}
                        />
                      </TableCell>
                      <TableCell>
                        {field.selected && (
                          <>
                            <IconButton
                              size="small"
                              onClick={() => moveField(field.fieldName, 'up')}
                              disabled={selectedFields[0]?.fieldName === field.fieldName}
                            >
                              <ArrowUpwardIcon fontSize="small" />
                            </IconButton>
                            <IconButton
                              size="small"
                              onClick={() => moveField(field.fieldName, 'down')}
                              disabled={selectedFields[selectedFields.length - 1]?.fieldName === field.fieldName}
                            >
                              <ArrowDownwardIcon fontSize="small" />
                            </IconButton>
                          </>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSave} variant="contained" disabled={saving}>
          {saving ? 'Saving...' : 'Save'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
