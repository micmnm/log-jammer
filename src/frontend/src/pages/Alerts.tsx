import { useState } from 'react';
import { Box, Typography, Tabs, Tab, Pagination, Stack } from '@mui/material';
import { useAlertHistory } from '../api/hooks/useAlerts';
import AlertsFeed from '../components/AlertsFeed';
import AlertCard from '../components/AlertCard';

export default function Alerts() {
  const [tab, setTab] = useState(0);
  const [historyPage, setHistoryPage] = useState(1);
  const pageSize = 20;

  const { data: history, isLoading: historyLoading } = useAlertHistory(historyPage, pageSize);

  const totalPages = history ? Math.ceil(history.totalCount / pageSize) : 0;

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 2 }}>
        Alerts
      </Typography>
      <Tabs value={tab} onChange={(_, v: number) => setTab(v)} sx={{ mb: 3 }}>
        <Tab label="Active" />
        <Tab label="History" />
      </Tabs>

      {tab === 0 && <AlertsFeed />}

      {tab === 1 && (
        <Box>
          {historyLoading ? (
            <Typography>Loading...</Typography>
          ) : history && history.items.length > 0 ? (
            <>
              {history.items.map((alert) => (
                <AlertCard key={alert.id} alert={alert} showAcknowledge={false} />
              ))}
              {totalPages > 1 && (
                <Stack alignItems="center" sx={{ mt: 2 }}>
                  <Pagination
                    count={totalPages}
                    page={historyPage}
                    onChange={(_, p) => setHistoryPage(p)}
                  />
                </Stack>
              )}
            </>
          ) : (
            <Typography variant="body2" color="text.secondary">
              No resolved alerts.
            </Typography>
          )}
        </Box>
      )}
    </Box>
  );
}
