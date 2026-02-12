import { useState } from 'react';
import { Box, Typography, Tabs, Tab } from '@mui/material';
import RulesTab from '../components/settings/RulesTab';
import TagsTab from '../components/settings/TagsTab';
import ClassificationTab from '../components/settings/ClassificationTab';

export default function Settings() {
  const [tab, setTab] = useState(0);

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 2 }}>
        Settings
      </Typography>
      <Tabs value={tab} onChange={(_, v: number) => setTab(v)} sx={{ mb: 3 }}>
        <Tab label="Rules" />
        <Tab label="Tags" />
        <Tab label="Classification" />
      </Tabs>

      {tab === 0 && <RulesTab />}
      {tab === 1 && <TagsTab />}
      {tab === 2 && <ClassificationTab />}
    </Box>
  );
}
