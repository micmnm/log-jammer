import { useState, useEffect } from 'react';
import Box from '@mui/material/Box';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Typography from '@mui/material/Typography';
import RecentQueries from './components/RecentQueries';
import ActiveSubscriptions from './components/ActiveSubscriptions';
import Settings from './components/Settings';
import type { CapturedQuery, Subscription, ExtensionSettings } from '../shared/types';

export default function App() {
  const [tab, setTab] = useState(0);
  const [queries, setQueries] = useState<CapturedQuery[]>([]);
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
  const [settings, setSettings] = useState<ExtensionSettings | null>(null);

  const refreshState = () => {
    chrome.runtime.sendMessage({ type: 'GET_STATE' }, (response) => {
      if (response) {
        setQueries(response.queries ?? []);
        setSubscriptions(response.subscriptions ?? []);
        setSettings(response.settings ?? null);
      }
    });
  };

  useEffect(() => { refreshState(); }, []);

  return (
    <Box sx={{ width: '100%' }}>
      <Box sx={{ px: 2, pt: 1.5, pb: 0.5, display: 'flex', alignItems: 'center', gap: 1 }}>
        <Typography variant="subtitle1" fontWeight={700} color="primary">
          Log Jammer
        </Typography>
        <Typography variant="caption" color="text.secondary">
          Kibana Bridge
        </Typography>
      </Box>
      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="fullWidth" sx={{ minHeight: 36 }}>
        <Tab label={`Queries (${queries.length})`} sx={{ minHeight: 36, py: 0 }} />
        <Tab label={`Active (${subscriptions.length})`} sx={{ minHeight: 36, py: 0 }} />
        <Tab label="Settings" sx={{ minHeight: 36, py: 0 }} />
      </Tabs>
      <Box sx={{ p: 1.5 }}>
        {tab === 0 && <RecentQueries queries={queries} onSubscribe={refreshState} />}
        {tab === 1 && <ActiveSubscriptions subscriptions={subscriptions} onUpdate={refreshState} />}
        {tab === 2 && settings && <Settings settings={settings} onSave={refreshState} />}
      </Box>
    </Box>
  );
}
