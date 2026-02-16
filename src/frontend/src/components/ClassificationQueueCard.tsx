import { useState } from 'react';
import {
  Card,
  CardContent,
  CardActions,
  Typography,
  Button,
  Chip,
  Stack,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Autocomplete,
  TextField,
  Box,
} from '@mui/material';
import { useTheme } from '@mui/material/styles';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ConfidenceBar from './ConfidenceBar';
import { useApproveClassification, useRejectClassification } from '../api/hooks/useClassification';
import { useTags } from '../api/hooks/useTags';
import type { ClassificationQueueResponse, TagResponse } from '../api/types';

interface ClassificationQueueCardProps {
  item: ClassificationQueueResponse;
}

export default function ClassificationQueueCard({ item }: ClassificationQueueCardProps) {
  const theme = useTheme();
  const [rejectOpen, setRejectOpen] = useState(false);
  const [selectedTags, setSelectedTags] = useState<TagResponse[]>([]);
  const [reason, setReason] = useState('');
  const [expanded, setExpanded] = useState(false);

  const approve = useApproveClassification();
  const reject = useRejectClassification();
  const { data: allTags } = useTags();

  const handleApprove = () => {
    approve.mutate({
      id: item.id,
      tagIds: item.suggestedTags.map((t) => t.tagId),
    });
  };

  const handleReject = () => {
    reject.mutate(
      {
        id: item.id,
        correctTagIds: selectedTags.map((t) => t.id),
        reason: reason || undefined,
      },
      {
        onSuccess: () => {
          setRejectOpen(false);
          setSelectedTags([]);
          setReason('');
        },
      },
    );
  };

  return (
    <>
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent>
          <Typography
            variant="body1"
            sx={{
              mb: 1,
              cursor: 'pointer',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              display: '-webkit-box',
              WebkitLineClamp: expanded ? 'unset' : 3,
              WebkitBoxOrient: 'vertical',
            }}
            onClick={() => setExpanded(!expanded)}
          >
            {item.message}
          </Typography>

          {item.confidence != null && (
            <Box sx={{ mb: 1, maxWidth: 300 }}>
              <Typography variant="caption" color="text.secondary">
                Overall confidence
              </Typography>
              <ConfidenceBar value={item.confidence} />
            </Box>
          )}

          <Stack direction="row" spacing={1} flexWrap="wrap" sx={{ mb: 1 }}>
            {item.suggestedTags.map((tag) => (
              <Box key={tag.tagId} sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                <Chip label={tag.tagName} size="small" color="primary" variant="outlined" />
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ fontFamily: theme.fontFamilyMono, fontSize: '0.7rem' }}
                >
                  {Math.round(tag.confidence * 100)}%
                </Typography>
              </Box>
            ))}
          </Stack>

          {item.stackTrace && (
            <Accordion variant="outlined" sx={{ mt: 1 }}>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Typography variant="body2">Stack Trace</Typography>
              </AccordionSummary>
              <AccordionDetails>
                <Box
                  component="pre"
                  sx={{
                    fontFamily: theme.fontFamilyMono,
                    fontSize: '0.75rem',
                    overflow: 'auto',
                    maxHeight: 300,
                    m: 0,
                  }}
                >
                  {item.stackTrace}
                </Box>
              </AccordionDetails>
            </Accordion>
          )}
        </CardContent>
        <CardActions>
          <Button
            size="small"
            variant="contained"
            color="success"
            onClick={handleApprove}
            disabled={approve.isPending}
          >
            Approve
          </Button>
          <Button
            size="small"
            variant="outlined"
            color="error"
            onClick={() => setRejectOpen(true)}
          >
            Reject
          </Button>
        </CardActions>
      </Card>

      <Dialog open={rejectOpen} onClose={() => setRejectOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Reject Classification</DialogTitle>
        <DialogContent>
          <Autocomplete
            multiple
            options={allTags ?? []}
            getOptionLabel={(opt) => opt.name}
            value={selectedTags}
            onChange={(_, value) => setSelectedTags(value)}
            renderInput={(params) => (
              <TextField {...params} label="Correct Tags" margin="normal" />
            )}
          />
          <TextField
            label="Reason (optional)"
            fullWidth
            multiline
            rows={2}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            margin="normal"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRejectOpen(false)}>Cancel</Button>
          <Button
            onClick={handleReject}
            variant="contained"
            color="error"
            disabled={reject.isPending || selectedTags.length === 0}
          >
            Confirm Reject
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
