import { useState } from 'react';
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
  Switch,
  IconButton,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
  Typography,
} from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import AddIcon from '@mui/icons-material/Add';
import {
  useSpikeDetectionRules,
  useDeleteSpikeDetectionRule,
  useUpdateSpikeDetectionRule,
} from '../../api/hooks/useSpikeDetectionRules';
import type { SpikeDetectionRuleDto } from '../../api/types';
import RuleDialog from './RuleDialog';

export default function RulesTab() {
  const { data: rules, isLoading } = useSpikeDetectionRules();
  const deleteRule = useDeleteSpikeDetectionRule();
  const updateRule = useUpdateSpikeDetectionRule();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<SpikeDetectionRuleDto | null>(null);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  const handleAdd = () => {
    setEditingRule(null);
    setDialogOpen(true);
  };

  const handleEdit = (rule: SpikeDetectionRuleDto) => {
    setEditingRule(rule);
    setDialogOpen(true);
  };

  const handleDelete = () => {
    if (deleteConfirmId) {
      deleteRule.mutate(deleteConfirmId);
      setDeleteConfirmId(null);
    }
  };

  const handleToggleEnabled = (rule: SpikeDetectionRuleDto) => {
    updateRule.mutate({ id: rule.id, request: { enabled: !rule.enabled } });
  };

  if (isLoading) return <Typography>Loading...</Typography>;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mb: 2 }}>
        <Button variant="contained" startIcon={<AddIcon />} onClick={handleAdd}>
          Add Rule
        </Button>
      </Box>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Target</TableCell>
              <TableCell>Threshold Type</TableCell>
              <TableCell>Value</TableCell>
              <TableCell>Window</TableCell>
              <TableCell>Lookback</TableCell>
              <TableCell>Enabled</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rules?.map((rule) => (
              <TableRow key={rule.id}>
                <TableCell>{rule.knownErrorMessage ?? 'Global Default'}</TableCell>
                <TableCell>{rule.thresholdType}</TableCell>
                <TableCell>{rule.thresholdValue}</TableCell>
                <TableCell>{rule.windowMinutes}m</TableCell>
                <TableCell>{rule.lookbackMinutes}m</TableCell>
                <TableCell>
                  <Switch checked={rule.enabled} onChange={() => handleToggleEnabled(rule)} size="small" />
                </TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => handleEdit(rule)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                  <IconButton size="small" onClick={() => setDeleteConfirmId(rule.id)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {rules?.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} align="center">
                  <Typography variant="body2" color="text.secondary">
                    No spike detection rules configured.
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <RuleDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        rule={editingRule}
      />

      <Dialog open={!!deleteConfirmId} onClose={() => setDeleteConfirmId(null)}>
        <DialogTitle>Delete Rule</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to delete this spike detection rule?
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
