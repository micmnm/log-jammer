import { useState } from 'react';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import FormControlLabel from '@mui/material/FormControlLabel';
import Switch from '@mui/material/Switch';
import type { ExtensionSettings } from '../../shared/types';

interface Props {
  settings: ExtensionSettings;
  onSave: () => void;
}

export default function Settings({ settings, onSave }: Props) {
  const [url, setUrl] = useState(settings.logJammerUrl);
  const [apiKey, setApiKey] = useState(settings.apiKey);
  const [maxQueries, setMaxQueries] = useState(String(settings.maxCapturedQueries));
  const [pollInterval, setPollInterval] = useState(String(settings.defaultPollIntervalMinutes ?? 5));
  const [verbose, setVerbose] = useState(settings.verbose ?? false);
  const [errorDetails, setErrorDetails] = useState(settings.errorDetails ?? false);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);

  function markDirty() { setSaved(false); }

  const handleSave = () => {
    setSaving(true);
    chrome.runtime.sendMessage(
      {
        type: 'UPDATE_SETTINGS',
        payload: {
          logJammerUrl: url.replace(/\/+$/, ''), // trim trailing slash
          apiKey: apiKey.trim(),
          maxCapturedQueries: parseInt(maxQueries, 10) || 50,
          defaultPollIntervalMinutes: parseFloat(pollInterval) || 5,
          verbose,
          errorDetails,
        },
      },
      () => {
        setSaving(false);
        setSaved(true);
        onSave();
      }
    );
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <TextField
        label="Log Jammer URL"
        value={url}
        onChange={(e) => { setUrl(e.target.value); markDirty(); }}
        size="small"
        fullWidth
        placeholder="https://logjammer.mltru.com"
        helperText="The URL of your Log Jammer instance"
      />
      <TextField
        label="API Key"
        value={apiKey}
        onChange={(e) => { setApiKey(e.target.value); markDirty(); }}
        size="small"
        fullWidth
        type="password"
        placeholder="Optional — required if auth is enabled"
        helperText="API key sent as X-Api-Key header"
      />
      <TextField
        label="Max captured queries"
        type="number"
        value={maxQueries}
        onChange={(e) => { setMaxQueries(e.target.value); markDirty(); }}
        size="small"
        fullWidth
        slotProps={{ htmlInput: { min: 10, max: 200 } }}
      />
      <TextField
        label="Default poll interval (minutes)"
        type="number"
        value={pollInterval}
        onChange={(e) => { setPollInterval(e.target.value); markDirty(); }}
        size="small"
        fullWidth
        slotProps={{ htmlInput: { min: 0.5, max: 60, step: 0.5 } }}
        helperText="How often subscriptions poll Kibana (min 0.5m)"
      />
      <FormControlLabel
        control={<Switch checked={verbose} onChange={(e) => { setVerbose(e.target.checked); markDirty(); }} />}
        label="Verbose logging"
      />
      <Typography variant="caption" color="text.secondary" sx={{ mt: -1 }}>
        Detailed console logs for interceptor and polling
      </Typography>
      <FormControlLabel
        control={<Switch checked={errorDetails} onChange={(e) => { setErrorDetails(e.target.checked); markDirty(); }} />}
        label="Error details"
      />
      <Typography variant="caption" color="text.secondary" sx={{ mt: -1 }}>
        On poll failure, log original captured URL/payload vs actual request URL/payload
      </Typography>
      <Button
        variant="contained"
        onClick={handleSave}
        disabled={saving}
        color={saved ? 'success' : 'primary'}
      >
        {saving ? 'Saving…' : saved ? 'Saved ✓' : 'Save Settings'}
      </Button>

      <Box sx={{ mt: 2, pt: 2, borderTop: 1, borderColor: 'divider' }}>
        <Typography variant="caption" color="text.secondary">
          Log Jammer Kibana Bridge v2.0.0
        </Typography>
      </Box>
    </Box>
  );
}
