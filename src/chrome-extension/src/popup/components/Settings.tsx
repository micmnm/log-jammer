import { useState } from 'react';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import type { ExtensionSettings } from '../../shared/types';

interface Props {
  settings: ExtensionSettings;
  onSave: () => void;
}

export default function Settings({ settings, onSave }: Props) {
  const [url, setUrl] = useState(settings.logJammerUrl);
  const [maxQueries, setMaxQueries] = useState(String(settings.maxCapturedQueries));
  const [saved, setSaved] = useState(false);

  const handleSave = () => {
    chrome.runtime.sendMessage(
      {
        type: 'UPDATE_SETTINGS',
        payload: {
          logJammerUrl: url.replace(/\/+$/, ''), // trim trailing slash
          maxCapturedQueries: parseInt(maxQueries, 10) || 50,
        },
      },
      () => {
        setSaved(true);
        setTimeout(() => setSaved(false), 2000);
        onSave();
      }
    );
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <TextField
        label="Log Jammer URL"
        value={url}
        onChange={(e) => setUrl(e.target.value)}
        size="small"
        fullWidth
        placeholder="http://localhost:5050"
        helperText="The URL of your Log Jammer instance"
      />
      <TextField
        label="Max captured queries"
        type="number"
        value={maxQueries}
        onChange={(e) => setMaxQueries(e.target.value)}
        size="small"
        fullWidth
        slotProps={{ htmlInput: { min: 10, max: 200 } }}
      />
      <Button variant="contained" onClick={handleSave}>
        Save Settings
      </Button>
      {saved && <Alert severity="success" sx={{ py: 0 }}>Settings saved</Alert>}

      <Box sx={{ mt: 2, pt: 2, borderTop: 1, borderColor: 'divider' }}>
        <Typography variant="caption" color="text.secondary">
          Log Jammer Kibana Bridge v0.1.0
        </Typography>
      </Box>
    </Box>
  );
}
