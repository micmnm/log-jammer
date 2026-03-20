import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import DeleteIcon from '@mui/icons-material/Delete';
import PauseIcon from '@mui/icons-material/Pause';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import type { Subscription } from '../../shared/types';

interface Props {
  subscriptions: Subscription[];
  onUpdate: () => void;
}

const statusColors: Record<Subscription['status'], 'success' | 'warning' | 'error'> = {
  active: 'success',
  paused: 'warning',
  error: 'error',
};

export default function ActiveSubscriptions({ subscriptions, onUpdate }: Props) {
  const handleDelete = (id: string) => {
    chrome.runtime.sendMessage({ type: 'UNSUBSCRIBE', payload: { subscriptionId: id } }, () => {
      onUpdate();
    });
  };

  const handlePause = (id: string) => {
    chrome.runtime.sendMessage({ type: 'PAUSE_SUBSCRIPTION', payload: { subscriptionId: id } }, () => {
      onUpdate();
    });
  };

  const handleResume = (id: string) => {
    chrome.runtime.sendMessage({ type: 'RESUME_SUBSCRIPTION', payload: { subscriptionId: id } }, () => {
      onUpdate();
    });
  };

  if (subscriptions.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" textAlign="center" py={4}>
        No active subscriptions. Subscribe to a captured query to start feeding data.
      </Typography>
    );
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
      {subscriptions.map((sub) => (
        <Card key={sub.id} variant="outlined" sx={{ bgcolor: 'background.paper' }}>
          <CardContent sx={{ py: 1, px: 1.5, '&:last-child': { pb: 1 } }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.5 }}>
                  <Typography variant="body2" fontWeight={600} noWrap>
                    {sub.name}
                  </Typography>
                  <Chip label={sub.status} size="small" color={statusColors[sub.status]} />
                </Box>
                <Typography variant="caption" color="text.secondary" display="block">
                  Every {sub.pollIntervalMinutes} min
                  {sub.lastPollAt && ` · Last: ${new Date(sub.lastPollAt).toLocaleTimeString()}`}
                </Typography>
                {sub.messageTemplate && (
                  <Typography
                    variant="caption"
                    color="text.secondary"
                    display="block"
                    sx={{ fontFamily: 'monospace', fontSize: 10, mt: 0.25 }}
                  >
                    {sub.messageTemplate}
                  </Typography>
                )}
                {sub.lastError && (
                  <Typography
                    variant="caption"
                    color="error"
                    display="block"
                    sx={{ mt: 0.5, whiteSpace: 'pre-wrap', wordBreak: 'break-all', fontFamily: sub.lastError.includes('---') ? 'monospace' : undefined, fontSize: sub.lastError.includes('---') ? '0.65rem' : undefined }}
                  >
                    {sub.lastError}
                  </Typography>
                )}
              </Box>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.25, ml: 0.5 }}>
                {sub.status === 'active' ? (
                  <Tooltip title="Pause">
                    <IconButton size="small" onClick={() => handlePause(sub.id)} color="warning">
                      <PauseIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                ) : (
                  <Tooltip title="Resume">
                    <IconButton size="small" onClick={() => handleResume(sub.id)} color="success">
                      <PlayArrowIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
                <Tooltip title="Delete">
                  <IconButton size="small" onClick={() => handleDelete(sub.id)} color="error">
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Box>
            </Box>
          </CardContent>
        </Card>
      ))}
    </Box>
  );
}
