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
  FormControl,
  Select,
  MenuItem,
  Typography,
  Box,
  CircularProgress,
} from '@mui/material';
import type { DataSourceResponse } from '../api/types';
import { useDataSourceSchema, useSampleRecords, useUpdateDataSource } from '../api/hooks/useDataSources';

const TARGET_FIELDS = ['message', 'timestamp', 'severity', 'stack_trace'];

interface Props {
  open: boolean;
  onClose: () => void;
  dataSource: DataSourceResponse;
}

function parseMapping(json: string | null): Record<string, string> {
  if (!json) return {};
  try {
    return JSON.parse(json);
  } catch {
    return {};
  }
}

export default function SchemaMappingDialog({ open, onClose, dataSource }: Props) {
  const { data: schema, isLoading: schemaLoading } = useDataSourceSchema(dataSource.id);
  const { data: sampleData } = useSampleRecords(dataSource.id, 3);
  const updateDataSource = useUpdateDataSource();

  const [mapping, setMapping] = useState<Record<string, string>>({});

  useEffect(() => {
    if (open) {
      setMapping(parseMapping(dataSource.schemaMapping));
    }
  }, [open, dataSource.schemaMapping]);

  const handleFieldChange = (targetField: string, sourceField: string) => {
    setMapping((prev) => {
      const next = { ...prev };
      if (sourceField) {
        next[targetField] = sourceField;
      } else {
        delete next[targetField];
      }
      return next;
    });
  };

  const handleSave = () => {
    const schemaMapping = Object.keys(mapping).length > 0 ? JSON.stringify(mapping) : null;
    updateDataSource.mutate(
      { id: dataSource.id, request: { schemaMapping: schemaMapping ?? undefined } },
      { onSuccess: () => onClose() },
    );
  };

  const sourceFields = schema?.fields.map((f) => f.name) ?? [];

  // Build preview rows based on mapping + sample data
  const previewRows = sampleData?.records.slice(0, 3).map((record) => {
    const mapped: Record<string, unknown> = {};
    for (const target of TARGET_FIELDS) {
      const source = mapping[target];
      mapped[target] = source ? (record.fields[source] ?? '') : '';
    }
    return mapped;
  });

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Schema Mapping - {dataSource.name}</DialogTitle>
      <DialogContent>
        {schemaLoading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress />
          </Box>
        ) : (
          <>
            <TableContainer component={Paper} variant="outlined" sx={{ mb: 3 }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Target Field</TableCell>
                    <TableCell>Source Field</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {TARGET_FIELDS.map((target) => (
                    <TableRow key={target}>
                      <TableCell sx={{ fontWeight: 'bold' }}>{target}</TableCell>
                      <TableCell>
                        <FormControl size="small" fullWidth>
                          <Select
                            value={mapping[target] ?? ''}
                            onChange={(e) => handleFieldChange(target, e.target.value)}
                            displayEmpty
                          >
                            <MenuItem value="">
                              <em>None</em>
                            </MenuItem>
                            {sourceFields.map((field) => (
                              <MenuItem key={field} value={field}>
                                {field}
                              </MenuItem>
                            ))}
                          </Select>
                        </FormControl>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>

            {previewRows && previewRows.length > 0 && (
              <>
                <Typography variant="subtitle2" sx={{ mb: 1 }}>
                  Preview (sample records)
                </Typography>
                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        {TARGET_FIELDS.map((f) => (
                          <TableCell key={f}>{f}</TableCell>
                        ))}
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {previewRows.map((row, i) => (
                        <TableRow key={i}>
                          {TARGET_FIELDS.map((f) => (
                            <TableCell key={f}>
                              <Typography variant="body2" noWrap sx={{ maxWidth: 200 }}>
                                {String(row[f] ?? '')}
                              </Typography>
                            </TableCell>
                          ))}
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </>
            )}
          </>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSave} variant="contained" disabled={updateDataSource.isPending}>
          Save Mapping
        </Button>
      </DialogActions>
    </Dialog>
  );
}
