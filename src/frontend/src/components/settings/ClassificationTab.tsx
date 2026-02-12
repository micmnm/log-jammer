import { useState, useEffect } from 'react';
import {
  Box,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  TextField,
  Typography,
} from '@mui/material';
import { useConfiguration, useUpdateConfiguration } from '../../api/hooks/useConfiguration';

export default function ClassificationTab() {
  const { data: configs, isLoading } = useConfiguration();
  const updateConfig = useUpdateConfiguration();

  const [values, setValues] = useState<Record<string, string>>({});

  useEffect(() => {
    if (configs) {
      const map: Record<string, string> = {};
      for (const c of configs) {
        map[c.key] = c.value;
      }
      setValues(map);
    }
  }, [configs]);

  const handleChange = (key: string, value: string) => {
    setValues((prev) => ({ ...prev, [key]: value }));
  };

  const handleSaveAll = () => {
    for (const [key, value] of Object.entries(values)) {
      const original = configs?.find((c) => c.key === key);
      if (original && original.value !== value) {
        updateConfig.mutate({ key, value });
      }
    }
  };

  if (isLoading) return <Typography>Loading...</Typography>;

  return (
    <Box>
      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Key</TableCell>
              <TableCell>Value</TableCell>
              <TableCell>Description</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {configs?.map((config) => (
              <TableRow key={config.key}>
                <TableCell sx={{ fontWeight: 'bold' }}>{config.key}</TableCell>
                <TableCell>
                  <TextField
                    size="small"
                    value={values[config.key] ?? ''}
                    onChange={(e) => handleChange(config.key, e.target.value)}
                  />
                </TableCell>
                <TableCell>
                  <Typography variant="body2" color="text.secondary">
                    {config.description ?? ''}
                  </Typography>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 2 }}>
        <Button variant="contained" onClick={handleSaveAll} disabled={updateConfig.isPending}>
          Save All
        </Button>
      </Box>
    </Box>
  );
}
