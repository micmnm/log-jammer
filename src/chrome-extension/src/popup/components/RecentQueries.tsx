import { useState } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import type { CapturedQuery } from '../../shared/types';

interface Props {
  queries: CapturedQuery[];
  onSubscribe: () => void;
}

export default function RecentQueries({ queries, onSubscribe }: Props) {
  const [subscribeTarget, setSubscribeTarget] = useState<CapturedQuery | null>(null);
  const [name, setName] = useState('');
  const [interval, setInterval] = useState('5');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubscribe = () => {
    if (!subscribeTarget) return;
    setLoading(true);
    setError(null);

    chrome.runtime.sendMessage(
      {
        type: 'SUBSCRIBE',
        payload: {
          queryId: subscribeTarget.id,
          name,
          pollIntervalMinutes: parseInt(interval, 10),
        },
      },
      (response) => {
        setLoading(false);
        if (response?.ok) {
          setSubscribeTarget(null);
          onSubscribe();
        } else {
          setError(response?.error ?? 'Unknown error');
        }
      }
    );
  };

  if (queries.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" textAlign="center" py={4}>
        No queries captured yet. Search in Kibana Discover to see queries here.
      </Typography>
    );
  }

  return (
    <>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        {queries.map((q) => (
          <Card key={q.id} variant="outlined" sx={{ bgcolor: 'background.paper' }}>
            <CardContent sx={{ py: 1, px: 1.5, '&:last-child': { pb: 1 } }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Chip label={q.indexPattern} size="small" color="primary" variant="outlined" sx={{ mb: 0.5 }} />
                  <Typography variant="body2" sx={{ fontFamily: 'monospace', fontSize: 11, wordBreak: 'break-all' }}>
                    {q.summary}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {new Date(q.capturedAt).toLocaleTimeString()}
                  </Typography>
                </Box>
                <Button
                  size="small"
                  variant="contained"
                  onClick={() => {
                    setSubscribeTarget(q);
                    setName(`${q.indexPattern} — ${q.summary}`.slice(0, 60));
                    setInterval('5');
                  }}
                  sx={{ ml: 1, whiteSpace: 'nowrap' }}
                >
                  Subscribe
                </Button>
              </Box>
            </CardContent>
          </Card>
        ))}
      </Box>

      <Dialog open={!!subscribeTarget} onClose={() => setSubscribeTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Subscribe to Query</DialogTitle>
        <DialogContent>
          <TextField
            label="Name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            fullWidth
            margin="normal"
            size="small"
          />
          <TextField
            label="Poll interval (minutes)"
            type="number"
            value={interval}
            onChange={(e) => setInterval(e.target.value)}
            fullWidth
            margin="normal"
            size="small"
            slotProps={{ htmlInput: { min: 1, max: 1440 } }}
          />
          {error && (
            <Typography color="error" variant="body2" sx={{ mt: 1 }}>
              {error}
            </Typography>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSubscribeTarget(null)}>Cancel</Button>
          <Button
            onClick={handleSubscribe}
            variant="contained"
            disabled={loading || !name.trim()}
          >
            {loading ? 'Creating...' : 'Subscribe'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
