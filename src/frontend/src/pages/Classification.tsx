import { useState } from 'react';
import { Box, Typography, Pagination, CircularProgress, Alert } from '@mui/material';
import { useClassificationQueue } from '../api/hooks/useClassification';
import ClassificationQueueCard from '../components/ClassificationQueueCard';

export default function Classification() {
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const { data, isLoading, error } = useClassificationQueue(page, pageSize);

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 2 }}>
        Classification Queue
      </Typography>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
          <CircularProgress />
        </Box>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Failed to load classification queue: {error.message}
        </Alert>
      )}

      {data?.items.length === 0 && !isLoading && (
        <Typography color="text.secondary">No items in the classification queue.</Typography>
      )}

      {data?.items.map((item) => (
        <ClassificationQueueCard key={item.id} item={item} />
      ))}

      {data && data.totalCount > pageSize && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
          <Pagination
            count={Math.ceil(data.totalCount / pageSize)}
            page={page}
            onChange={(_, value) => setPage(value)}
            color="primary"
          />
        </Box>
      )}
    </Box>
  );
}
