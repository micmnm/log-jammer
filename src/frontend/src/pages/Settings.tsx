import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import DownloadIcon from '@mui/icons-material/Download';
import ExtensionIcon from '@mui/icons-material/Extension';

const installSteps = [
  'Download the extension zip file using the button below.',
  'Unzip the downloaded file to a folder on your computer.',
  'Open Chrome and navigate to chrome://extensions.',
  'Enable "Developer mode" using the toggle in the top-right corner.',
  'Click "Load unpacked" and select the unzipped folder.',
  'The Log Jammer extension icon will appear in your toolbar.',
  'Click the extension icon and configure the Log Jammer URL and API key in the Settings tab.',
];

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

        <Box component="ol" sx={{ pl: 2.5, m: 0 }}>
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
      </Paper>
    </Box>
  );
}
