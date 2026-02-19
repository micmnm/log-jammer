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
  Paper,
  LinearProgress,
  Tooltip,
  createFilterOptions,
} from '@mui/material';
import { useTheme } from '@mui/material/styles';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import LabelIcon from '@mui/icons-material/Label';
import StarIcon from '@mui/icons-material/Star';
import StorageIcon from '@mui/icons-material/Storage';
import { useApproveClassification, useRejectClassification } from '../api/hooks/useClassification';
import { useTags, useCreateTag } from '../api/hooks/useTags';
import type { ClassificationQueueResponse, TagResponse } from '../api/types';

const SEVERITY_COLORS: Record<string, string> = {
  Critical: '#ff1744',
  Warning: '#ffb300',
  Info: '#29b6f6',
};

const COLOR_PRESETS = [
  '#f44336', '#e91e63', '#9c27b0', '#673ab7',
  '#3f51b5', '#2196f3', '#03a9f4', '#00bcd4',
  '#009688', '#4caf50', '#8bc34a', '#cddc39',
  '#ffeb3b', '#ffc107', '#ff9800', '#ff5722',
];

interface TagOption {
  tag?: TagResponse;
  inputValue?: string;
  label: string;
}

const filterOptions = createFilterOptions<TagOption>();

function formatRelativeTime(dateStr: string): string {
  const now = Date.now();
  const then = new Date(dateStr).getTime();
  const diff = now - then;
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days}d ago`;
  return `${Math.floor(days / 30)}mo ago`;
}

interface ClassificationQueueCardProps {
  item: ClassificationQueueResponse;
}

export default function ClassificationQueueCard({ item }: ClassificationQueueCardProps) {
  const theme = useTheme();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'reject' | 'assign'>('reject');
  const [selectedTags, setSelectedTags] = useState<TagResponse[]>([]);
  const [reason, setReason] = useState('');

  // Inline tag creation state
  const [creatingTagName, setCreatingTagName] = useState<string | null>(null);
  const [creatingTagColor, setCreatingTagColor] = useState('#2196f3');

  const approve = useApproveClassification();
  const reject = useRejectClassification();
  const createTag = useCreateTag();
  const { data: allTags } = useTags();

  const hasSuggestions = item.suggestedTags.length > 0;
  const bestTag = hasSuggestions
    ? item.suggestedTags.reduce((best, t) => (t.confidence > best.confidence ? t : best))
    : null;
  const severityColor = SEVERITY_COLORS[item.severity] ?? '#29b6f6';

  const borderColor = hasSuggestions
    ? item.confidence != null
      ? item.confidence >= 0.7
        ? '#00e676'
        : item.confidence >= 0.4
          ? '#ffb300'
          : '#ff1744'
      : 'rgba(255,255,255,0.1)'
    : 'rgba(255,255,255,0.15)';

  const handleAcceptTags = () => {
    approve.mutate({
      id: item.id,
      tagIds: item.suggestedTags.map((t) => t.tagId),
    });
  };

  const handleAcceptTopTag = () => {
    if (!bestTag) return;
    approve.mutate({
      id: item.id,
      tagIds: [bestTag.tagId],
    });
  };

  const openDialog = (mode: 'reject' | 'assign') => {
    setDialogMode(mode);
    setDialogOpen(true);
    setSelectedTags([]);
    setReason('');
    setCreatingTagName(null);
  };

  const handleDialogSubmit = () => {
    if (dialogMode === 'reject') {
      reject.mutate(
        {
          id: item.id,
          correctTagIds: selectedTags.map((t) => t.id),
          reason: reason || undefined,
        },
        { onSuccess: () => setDialogOpen(false) },
      );
    } else {
      approve.mutate(
        {
          id: item.id,
          tagIds: selectedTags.map((t) => t.id),
        },
        { onSuccess: () => setDialogOpen(false) },
      );
    }
  };

  const handleCreateTag = () => {
    if (!creatingTagName) return;
    createTag.mutate(
      { name: creatingTagName, color: creatingTagColor },
      {
        onSuccess: (newTag) => {
          setSelectedTags((prev) => [...prev, newTag]);
          setCreatingTagName(null);
          setCreatingTagColor('#2196f3');
        },
      },
    );
  };

  const tagOptions: TagOption[] = (allTags ?? []).map((t) => ({ tag: t, label: t.name }));

  return (
    <>
      <Card
        variant="outlined"
        sx={{
          mb: 2,
          borderLeft: `4px solid ${borderColor}`,
        }}
      >
        <CardContent sx={{ pb: 1 }}>
          {/* UNMATCHED badge for items with no suggestions */}
          {!hasSuggestions && (
            <Chip
              label="UNMATCHED"
              size="small"
              sx={{
                mb: 1,
                backgroundColor: 'rgba(255,255,255,0.06)',
                color: '#bdbdbd',
                fontWeight: 700,
                fontSize: '0.7rem',
                letterSpacing: '0.05em',
              }}
            />
          )}

          {/* Error message */}
          <Typography variant="body1" sx={{ mb: 0.5, fontWeight: 500 }}>
            {item.message}
          </Typography>

          {/* Error context line */}
          <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1.5 }}>
            <Chip
              label={item.severity}
              size="small"
              sx={{
                height: 20,
                fontSize: '0.7rem',
                fontWeight: 600,
                backgroundColor: `${severityColor}20`,
                color: severityColor,
                border: `1px solid ${severityColor}40`,
              }}
            />
            {item.dataSourceName && (
              <>
                <Chip
                  icon={<StorageIcon sx={{ fontSize: 14 }} />}
                  label={item.dataSourceName}
                  size="small"
                  sx={{
                    height: 20,
                    fontSize: '0.7rem',
                    fontWeight: 500,
                    backgroundColor: 'rgba(255,255,255,0.06)',
                    color: 'text.secondary',
                  }}
                />
                <Typography variant="caption" color="text.secondary">
                  &middot;
                </Typography>
              </>
            )}
            <Tooltip title={new Date(item.firstSeen).toLocaleString()} arrow>
              <Typography variant="caption" color="text.secondary" sx={{ cursor: 'default' }}>
                First seen {formatRelativeTime(item.firstSeen)}
              </Typography>
            </Tooltip>
            <Typography variant="caption" color="text.secondary">
              &middot;
            </Typography>
            <Typography
              variant="caption"
              sx={{
                fontFamily: theme.fontFamilyMono,
                color: 'text.secondary',
              }}
            >
              {item.totalOccurrences.toLocaleString()} occurrences
            </Typography>
          </Stack>

          {/* ML SUGGESTION box — only when suggestions exist */}
          {hasSuggestions && (
            <Paper
              variant="outlined"
              sx={{
                p: 1.5,
                mb: 1.5,
                backgroundColor: 'rgba(255,255,255,0.02)',
                borderColor: 'rgba(255,255,255,0.08)',
              }}
            >
              <Typography
                variant="overline"
                sx={{
                  fontSize: '0.65rem',
                  fontWeight: 700,
                  letterSpacing: '0.1em',
                  color: 'text.secondary',
                  mb: 1,
                  display: 'block',
                }}
              >
                ML Suggestion
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ mb: 1, display: 'block' }}>
                Classifier suggests:
              </Typography>

              <Stack spacing={0.75}>
                {item.suggestedTags.map((tag) => {
                  const pct = Math.round(tag.confidence * 100);
                  const barColor = pct >= 70 ? '#00e676' : pct >= 40 ? '#ffb300' : '#ff1744';
                  const isBest = bestTag != null && tag.tagId === bestTag.tagId;
                  return (
                    <Box key={tag.tagId} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, minWidth: 80 }}>
                        {isBest && (
                          <StarIcon sx={{ fontSize: 14, color: '#ffc107' }} />
                        )}
                        <Typography
                          variant="body2"
                          sx={{
                            fontWeight: isBest ? 700 : 500,
                            fontSize: '0.8rem',
                          }}
                        >
                          {tag.tagName}
                        </Typography>
                      </Box>
                      <LinearProgress
                        variant="determinate"
                        value={pct}
                        sx={{
                          flex: 1,
                          height: 8,
                          borderRadius: 4,
                          backgroundColor: 'rgba(255,255,255,0.06)',
                          '& .MuiLinearProgress-bar': {
                            backgroundColor: barColor,
                            borderRadius: 4,
                          },
                        }}
                      />
                      <Typography
                        variant="caption"
                        sx={{
                          minWidth: 32,
                          fontFamily: theme.fontFamilyMono,
                          fontWeight: 600,
                          fontSize: '0.75rem',
                          color: barColor,
                          textAlign: 'right',
                        }}
                      >
                        {pct}%
                      </Typography>
                    </Box>
                  );
                })}
              </Stack>

              {item.confidence != null && (
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ mt: 1, display: 'block', fontFamily: theme.fontFamilyMono }}
                >
                  Overall confidence: {Math.round(item.confidence * 100)}%
                </Typography>
              )}
            </Paper>
          )}

          {/* No suggestions message */}
          {!hasSuggestions && (
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
              No similar errors found in the classifier. Assign tags manually.
            </Typography>
          )}

          {/* Stack trace accordion */}
          {item.stackTrace && (
            <Accordion variant="outlined" sx={{ mt: 0.5 }}>
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

        <CardActions sx={{ px: 2, pb: 1.5 }}>
          {hasSuggestions ? (
            <>
              <Button
                size="small"
                variant="contained"
                color="success"
                startIcon={<CheckIcon />}
                onClick={handleAcceptTopTag}
                disabled={approve.isPending}
              >
                Accept &ldquo;{bestTag!.tagName}&rdquo; {Math.round(bestTag!.confidence * 100)}%
              </Button>
              {item.suggestedTags.length > 1 && (
                <Button
                  size="small"
                  variant="outlined"
                  color="success"
                  startIcon={<CheckIcon />}
                  onClick={handleAcceptTags}
                  disabled={approve.isPending}
                >
                  Accept All
                </Button>
              )}
              <Button
                size="small"
                variant="outlined"
                color="error"
                startIcon={<CloseIcon />}
                onClick={() => openDialog('reject')}
              >
                Reject &amp; Retag
              </Button>
            </>
          ) : (
            <Button
              size="small"
              variant="contained"
              startIcon={<LabelIcon />}
              onClick={() => openDialog('assign')}
            >
              Assign Tags
            </Button>
          )}
        </CardActions>
      </Card>

      {/* Reject / Assign dialog */}
      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>
          {dialogMode === 'reject' ? 'Reject & Retag' : 'Assign Tags'}
        </DialogTitle>
        <DialogContent>
          <Autocomplete
            multiple
            options={tagOptions}
            getOptionLabel={(opt) => opt.label}
            value={selectedTags.map((t) => ({ tag: t, label: t.name }))}
            isOptionEqualToValue={(option, value) => option.tag?.id === value.tag?.id}
            onChange={(_, value, changeReason, details) => {
              // Check if user selected a "Create" option
              const selected = details?.option;
              if (selected?.inputValue && changeReason === 'selectOption') {
                setCreatingTagName(selected.inputValue);
                setCreatingTagColor('#2196f3');
                return;
              }
              setSelectedTags(value.filter((v) => v.tag != null).map((v) => v.tag!));
            }}
            filterOptions={(options, params) => {
              const filtered = filterOptions(options, params);
              const { inputValue } = params;
              const exists = options.some((opt) => opt.label.toLowerCase() === inputValue.toLowerCase());
              if (inputValue !== '' && !exists) {
                filtered.push({
                  inputValue,
                  label: `Create "${inputValue}"`,
                });
              }
              return filtered;
            }}
            renderInput={(params) => (
              <TextField {...params} label="Tags" margin="normal" />
            )}
          />

          {/* Inline tag creation */}
          {creatingTagName && (
            <Paper
              variant="outlined"
              sx={{
                p: 1.5,
                mt: 1,
                backgroundColor: 'rgba(255,255,255,0.02)',
              }}
            >
              <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                Create tag &quot;{creatingTagName}&quot;
              </Typography>
              <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mb: 1 }}>
                {COLOR_PRESETS.map((c) => (
                  <Box
                    key={c}
                    onClick={() => setCreatingTagColor(c)}
                    sx={{
                      width: 24,
                      height: 24,
                      borderRadius: '50%',
                      backgroundColor: c,
                      cursor: 'pointer',
                      border: c === creatingTagColor ? '2px solid white' : '2px solid transparent',
                      '&:hover': { opacity: 0.8 },
                    }}
                  />
                ))}
              </Box>
              <Stack direction="row" spacing={1}>
                <Button
                  size="small"
                  variant="contained"
                  onClick={handleCreateTag}
                  disabled={createTag.isPending}
                >
                  Create
                </Button>
                <Button size="small" onClick={() => setCreatingTagName(null)}>
                  Cancel
                </Button>
              </Stack>
            </Paper>
          )}

          {dialogMode === 'reject' && (
            <TextField
              label="Reason (optional)"
              fullWidth
              multiline
              rows={2}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              margin="normal"
            />
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
          <Button
            onClick={handleDialogSubmit}
            variant="contained"
            color={dialogMode === 'reject' ? 'error' : 'primary'}
            disabled={
              (dialogMode === 'reject' ? reject.isPending : approve.isPending) ||
              selectedTags.length === 0
            }
          >
            {dialogMode === 'reject' ? 'Confirm Reject' : 'Confirm Assign'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
