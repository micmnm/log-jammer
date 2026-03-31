import { useState, useCallback, useMemo } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import InputAdornment from '@mui/material/InputAdornment';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import IconButton from '@mui/material/IconButton';
import Chip from '@mui/material/Chip';
import Alert from '@mui/material/Alert';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';

const TIMESTAMP_FIELDS = ['@timestamp', 'timestamp'];
const LEVEL_FIELDS = ['log.level', 'level', 'severity'];

function autoDetectLabel(fieldName: string): string | null {
  if (TIMESTAMP_FIELDS.includes(fieldName)) return 'timestamp';
  if (LEVEL_FIELDS.includes(fieldName)) return 'level';
  return null;
}

export interface FieldInfo {
  name: string;
  sampleValue: string;
}

interface Props {
  fields: FieldInfo[];
  selectedFields: string[];
  onChange: (fields: string[]) => void;
}

export default function FieldSelector({ fields, selectedFields, onChange }: Props) {
  const [search, setSearch] = useState('');

  const [order, setOrder] = useState<string[]>(() => {
    // Start with currently selected in their current order, then add remaining fields
    const selected = selectedFields.filter(f => fields.some(fi => fi.name === f));
    const rest = fields.map(f => f.name).filter(f => !selected.includes(f));
    return [...selected, ...rest];
  });

  const handleToggle = useCallback((fieldName: string) => {
    const next = selectedFields.includes(fieldName)
      ? selectedFields.filter(f => f !== fieldName)
      : [...selectedFields, fieldName].sort((a, b) => order.indexOf(a) - order.indexOf(b));
    onChange(next);
  }, [selectedFields, order, onChange]);

  const moveUp = useCallback((fieldName: string) => {
    setOrder(prev => {
      const idx = prev.indexOf(fieldName);
      if (idx <= 0) return prev;
      const next = [...prev];
      [next[idx - 1], next[idx]] = [next[idx], next[idx - 1]];
      return next;
    });
    // Re-sort selectedFields to match new order
    onChange(
      [...selectedFields].sort((a, b) => {
        const newOrder = [...order];
        const aIdx = newOrder.indexOf(a);
        const bIdx = newOrder.indexOf(b);
        if (a === fieldName || b === fieldName) {
          // Will be recalculated after state update
        }
        return aIdx - bIdx;
      })
    );
  }, [order, selectedFields, onChange]);

  const moveDown = useCallback((fieldName: string) => {
    setOrder(prev => {
      const idx = prev.indexOf(fieldName);
      if (idx < 0 || idx >= prev.length - 1) return prev;
      const next = [...prev];
      [next[idx], next[idx + 1]] = [next[idx + 1], next[idx]];
      return next;
    });
  }, []);

  const messagePreview = selectedFields
    .map(f => {
      const info = fields.find(fi => fi.name === f);
      return info?.sampleValue || `{${f}}`;
    })
    .filter(Boolean)
    .join(' | ');

  const showWarning = selectedFields.length > 6;

  // Show fields in the current order, filtered by search
  const orderedFields = order
    .map(name => fields.find(f => f.name === name))
    .filter((f): f is FieldInfo => f !== undefined);

  const filteredFields = useMemo(() => {
    if (!search) return orderedFields;
    const term = search.toLowerCase();
    return orderedFields.filter(
      f => selectedFields.includes(f.name) || f.name.toLowerCase().includes(term),
    );
  }, [orderedFields, search, selectedFields]);

  const selectedInOrder = order.filter(f => selectedFields.includes(f));

  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ mb: 0.5, display: 'block' }}>
        Select fields to include in the log message. Use arrows to reorder.
      </Typography>

      <TextField
        size="small"
        placeholder="Search fields…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        fullWidth
        sx={{ mb: 0.5 }}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon sx={{ fontSize: 16 }} />
              </InputAdornment>
            ),
            endAdornment: search ? (
              <InputAdornment position="end">
                <IconButton size="small" onClick={() => setSearch('')} sx={{ p: 0.25 }}>
                  <ClearIcon sx={{ fontSize: 14 }} />
                </IconButton>
              </InputAdornment>
            ) : null,
            sx: { fontSize: 12 },
          },
        }}
      />

      <Box sx={{ maxHeight: 400, overflowY: 'auto', border: 1, borderColor: 'divider', borderRadius: 1, mb: 1 }}>
        {filteredFields.map((field, idx) => {
          const isSelected = selectedFields.includes(field.name);
          const autoLabel = autoDetectLabel(field.name);
          const isFirst = idx === 0;
          const isLast = idx === filteredFields.length - 1;

          return (
            <Box
              key={field.name}
              sx={{
                display: 'flex',
                alignItems: 'center',
                px: 0.5,
                py: 0.25,
                borderBottom: idx < filteredFields.length - 1 ? 1 : 0,
                borderColor: 'divider',
                bgcolor: isSelected ? 'action.selected' : 'transparent',
              }}
            >
              <FormControlLabel
                control={
                  <Checkbox
                    checked={isSelected}
                    onChange={() => handleToggle(field.name)}
                    size="small"
                    sx={{ py: 0 }}
                  />
                }
                label={
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, flexWrap: 'wrap' }}>
                    <Typography variant="body2" sx={{ fontFamily: 'monospace', fontSize: 11 }}>
                      {field.name}
                    </Typography>
                    {autoLabel && (
                      <Chip
                        label={`auto-detected ${autoLabel}`}
                        size="small"
                        color="info"
                        variant="outlined"
                        sx={{ height: 16, fontSize: 9, '& .MuiChip-label': { px: 0.5 } }}
                      />
                    )}
                    {field.sampleValue && (
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{ fontSize: 10, maxWidth: 120, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                      >
                        = {field.sampleValue}
                      </Typography>
                    )}
                  </Box>
                }
                sx={{ flex: 1, mr: 0, my: 0 }}
              />
              <Box sx={{ display: 'flex', flexDirection: 'column' }}>
                <IconButton
                  size="small"
                  onClick={() => moveUp(field.name)}
                  disabled={isFirst}
                  sx={{ p: 0, width: 16, height: 16 }}
                >
                  <ArrowUpwardIcon sx={{ fontSize: 12 }} />
                </IconButton>
                <IconButton
                  size="small"
                  onClick={() => moveDown(field.name)}
                  disabled={isLast}
                  sx={{ p: 0, width: 16, height: 16 }}
                >
                  <ArrowDownwardIcon sx={{ fontSize: 12 }} />
                </IconButton>
              </Box>
            </Box>
          );
        })}
        {filteredFields.length === 0 && (
          <Typography variant="caption" color="text.secondary" sx={{ p: 1, display: 'block' }}>
            {search ? 'No fields match your search.' : 'No fields available. Subscribe to a query after Kibana returns results.'}
          </Typography>
        )}
      </Box>

      {showWarning && (
        <Alert severity="warning" sx={{ py: 0.25, mb: 1, fontSize: 11 }}>
          More than 6 fields selected — messages may become very long.
        </Alert>
      )}

      <Box sx={{ bgcolor: 'background.default', border: 1, borderColor: 'divider', borderRadius: 1, p: 1 }}>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.25 }}>
          Message preview:
        </Typography>
        <Typography
          variant="caption"
          sx={{ fontFamily: 'monospace', fontSize: 10, wordBreak: 'break-all', color: selectedFields.length === 0 ? 'text.disabled' : 'text.primary' }}
        >
          {selectedFields.length === 0
            ? '(select fields above)'
            : messagePreview || '(no sample values available)'}
        </Typography>
      </Box>

      {selectedInOrder.length > 0 && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5, fontSize: 10 }}>
          Template: {selectedInOrder.map(f => `{${f}}`).join(' | ')}
        </Typography>
      )}
    </Box>
  );
}
