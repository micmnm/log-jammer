import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Switch,
  FormControlLabel,
} from '@mui/material';
import type { SpikeDetectionRuleDto, ThresholdType } from '../../api/types';
import {
  useCreateSpikeDetectionRule,
  useUpdateSpikeDetectionRule,
} from '../../api/hooks/useSpikeDetectionRules';

interface Props {
  open: boolean;
  onClose: () => void;
  rule: SpikeDetectionRuleDto | null;
}

export default function RuleDialog({ open, onClose, rule }: Props) {
  const createRule = useCreateSpikeDetectionRule();
  const updateRule = useUpdateSpikeDetectionRule();
  const isEdit = !!rule;

  const [thresholdType, setThresholdType] = useState<ThresholdType>('Absolute');
  const [thresholdValue, setThresholdValue] = useState(10);
  const [windowMinutes, setWindowMinutes] = useState(5);
  const [lookbackMinutes, setLookbackMinutes] = useState(1440);
  const [enabled, setEnabled] = useState(true);

  useEffect(() => {
    if (open && rule) {
      setThresholdType(rule.thresholdType);
      setThresholdValue(rule.thresholdValue);
      setWindowMinutes(rule.windowMinutes);
      setLookbackMinutes(rule.lookbackMinutes);
      setEnabled(rule.enabled);
    } else if (open) {
      setThresholdType('Absolute');
      setThresholdValue(10);
      setWindowMinutes(5);
      setLookbackMinutes(1440);
      setEnabled(true);
    }
  }, [open, rule]);

  const handleSave = () => {
    if (isEdit) {
      updateRule.mutate(
        { id: rule.id, request: { thresholdType, thresholdValue, windowMinutes, lookbackMinutes, enabled } },
        { onSuccess: () => onClose() },
      );
    } else {
      createRule.mutate(
        { thresholdType, thresholdValue, windowMinutes, lookbackMinutes, enabled },
        { onSuccess: () => onClose() },
      );
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? 'Edit Rule' : 'Add Rule'}</DialogTitle>
      <DialogContent>
        <FormControl fullWidth margin="normal">
          <InputLabel>Threshold Type</InputLabel>
          <Select
            value={thresholdType}
            label="Threshold Type"
            onChange={(e) => setThresholdType(e.target.value as ThresholdType)}
          >
            <MenuItem value="Absolute">Absolute</MenuItem>
            <MenuItem value="PercentageIncrease">Percentage Increase</MenuItem>
            <MenuItem value="StandardDeviation">Standard Deviation</MenuItem>
          </Select>
        </FormControl>
        <TextField
          label="Threshold Value"
          type="number"
          value={thresholdValue}
          onChange={(e) => setThresholdValue(Number(e.target.value))}
          fullWidth
          margin="normal"
          slotProps={{ htmlInput: { min: 0.01, step: 0.01 } }}
        />
        <TextField
          label="Window (minutes)"
          type="number"
          value={windowMinutes}
          onChange={(e) => setWindowMinutes(Number(e.target.value))}
          fullWidth
          margin="normal"
          slotProps={{ htmlInput: { min: 1, max: 1440 } }}
        />
        <TextField
          label="Lookback (minutes)"
          type="number"
          value={lookbackMinutes}
          onChange={(e) => setLookbackMinutes(Number(e.target.value))}
          fullWidth
          margin="normal"
          slotProps={{ htmlInput: { min: 5, max: 10080 } }}
        />
        <FormControlLabel
          control={<Switch checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />}
          label="Enabled"
          sx={{ mt: 1 }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSave} variant="contained">
          {isEdit ? 'Save' : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
