import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import DownloadIcon from '@mui/icons-material/Download';
import ExtensionIcon from '@mui/icons-material/Extension';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { useState } from 'react';

const baseUrl = window.location.origin;

const installSteps = [
  'Download the extension zip file using the button below.',
  'Unzip the downloaded file to a folder on your computer.',
  'Open Chrome and navigate to chrome://extensions.',
  'Enable "Developer mode" using the toggle in the top-right corner.',
  'Click "Load unpacked" and select the unzipped folder.',
  'The Log Jammer extension icon will appear in your toolbar.',
  'Click the extension icon, go to the Settings tab, and enter the configuration values shown below.',
];

function CopyableField({ label, value }: { label: string; value: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    void navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        px: 2,
        py: 1,
        bgcolor: 'action.hover',
        borderRadius: 1,
      }}
    >
      <Box>
        <Typography variant="caption" color="text.secondary">
          {label}
        </Typography>
        <Typography
          variant="body2"
          sx={{ fontFamily: 'monospace', fontWeight: 500 }}
        >
          {value}
        </Typography>
      </Box>
      <Tooltip title={copied ? 'Copied!' : 'Copy'}>
        <IconButton size="small" onClick={handleCopy}>
          <ContentCopyIcon fontSize="small" />
        </IconButton>
      </Tooltip>
    </Box>
  );
}

export default function Settings() {
  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 3, fontWeight: 600 }}>
        Settings
      </Typography>

      <Paper sx={{ p: 3, maxWidth: 720 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 2 }}>
          <ExtensionIcon color="primary" />
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            Chrome Extension
          </Typography>
        </Box>

        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          The Log Jammer Chrome extension captures Kibana queries and forwards
          them to your Log Jammer instance for pattern analysis. It runs as an
          unpacked extension installed in developer mode.
        </Typography>

        <Button
          variant="contained"
          startIcon={<DownloadIcon />}
          href="/downloads/log-jammer-extension.zip"
          download
          sx={{ mb: 3 }}
        >
          Download Extension
        </Button>

        <Divider sx={{ mb: 2 }} />

        <Typography variant="subtitle2" sx={{ mb: 1.5, fontWeight: 600 }}>
          Installation steps
        </Typography>

        <Box component="ol" sx={{ pl: 2.5, m: 0, mb: 3 }}>
          {installSteps.map((step, i) => (
            <Typography
              key={i}
              component="li"
              variant="body2"
              color="text.secondary"
              sx={{ mb: 1 }}
            >
              {step}
            </Typography>
          ))}
        </Box>

        <Divider sx={{ mb: 2 }} />

        <Typography variant="subtitle2" sx={{ mb: 1.5, fontWeight: 600 }}>
          Extension configuration
        </Typography>

        <Alert severity="info" sx={{ mb: 2 }}>
          Enter these values in the extension's Settings tab after installation.
        </Alert>

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <CopyableField label="Log Jammer URL" value={baseUrl} />
          <CopyableField
            label="API Key"
            value="(use the API key configured for this instance)"
          />
        </Box>
      </Paper>
    </Box>
  );
}
