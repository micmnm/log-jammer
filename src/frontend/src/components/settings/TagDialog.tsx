import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Box,
} from '@mui/material';
import type { TagResponse } from '../../api/types';
import { useCreateTag, useUpdateTag } from '../../api/hooks/useTags';

interface Props {
  open: boolean;
  onClose: () => void;
  tag: TagResponse | null;
}

export default function TagDialog({ open, onClose, tag }: Props) {
  const createTag = useCreateTag();
  const updateTag = useUpdateTag();
  const isEdit = !!tag;

  const [name, setName] = useState('');
  const [color, setColor] = useState('#1976d2');

  useEffect(() => {
    if (open && tag) {
      setName(tag.name);
      setColor(tag.color ?? '#1976d2');
    } else if (open) {
      setName('');
      setColor('#1976d2');
    }
  }, [open, tag]);

  const handleSave = () => {
    if (isEdit) {
      updateTag.mutate(
        { id: tag.id, request: { name, color } },
        { onSuccess: () => onClose() },
      );
    } else {
      createTag.mutate(
        { name, color },
        { onSuccess: () => onClose() },
      );
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{isEdit ? 'Edit Tag' : 'Add Tag'}</DialogTitle>
      <DialogContent>
        <TextField
          label="Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          fullWidth
          margin="normal"
          required
        />
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mt: 1 }}>
          <TextField
            label="Color"
            value={color}
            onChange={(e) => setColor(e.target.value)}
            sx={{ flex: 1 }}
          />
          <input
            type="color"
            value={color}
            onChange={(e) => setColor(e.target.value)}
            style={{ width: 48, height: 48, border: 'none', cursor: 'pointer' }}
          />
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSave} variant="contained" disabled={!name}>
          {isEdit ? 'Save' : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
