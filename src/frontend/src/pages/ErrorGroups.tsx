import { useState, useCallback } from 'react';
import { Box, Typography, FormControl, InputLabel, Select, MenuItem, Stack } from '@mui/material';
import { DataGrid, type GridColDef, type GridPaginationModel } from '@mui/x-data-grid';
import { useNavigate } from 'react-router-dom';
import { useErrorGroups } from '../api/hooks/useErrorGroups';
import { useDataSources } from '../api/hooks/useDataSources';
import SeverityChip from '../components/SeverityChip';
import StatusChip from '../components/StatusChip';
import type { ErrorSeverity, ErrorStatus, ErrorGroupResponse } from '../api/types';

const columns: GridColDef<ErrorGroupResponse>[] = [
  {
    field: 'representativeMessage',
    headerName: 'Message',
    flex: 2,
    minWidth: 250,
  },
  {
    field: 'severity',
    headerName: 'Severity',
    width: 120,
    renderCell: (params) => <SeverityChip severity={params.value} />,
  },
  {
    field: 'status',
    headerName: 'Status',
    width: 120,
    renderCell: (params) => <StatusChip status={params.value} />,
  },
  {
    field: 'dataSourceName',
    headerName: 'Data Source',
    width: 160,
    valueGetter: (_value, row) => row.dataSourceName ?? 'Unknown',
  },
  {
    field: 'totalOccurrences',
    headerName: 'Occurrences',
    width: 120,
    type: 'number',
  },
  {
    field: 'lastSeen',
    headerName: 'Last Seen',
    width: 180,
    valueFormatter: (value: string) => new Date(value).toLocaleString(),
  },
];

export default function ErrorGroups() {
  const navigate = useNavigate();
  const [severity, setSeverity] = useState<ErrorSeverity | ''>('');
  const [status, setStatus] = useState<ErrorStatus | ''>('');
  const [dataSourceId, setDataSourceId] = useState('');
  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 25,
  });

  const { data, isLoading } = useErrorGroups({
    severity: severity || undefined,
    status: status || undefined,
    dataSourceId: dataSourceId || undefined,
    page: paginationModel.page + 1,
    pageSize: paginationModel.pageSize,
  });

  const { data: dataSources } = useDataSources();

  const handleRowClick = useCallback(
    (params: { id: string | number }) => {
      navigate(`/error-groups/${params.id}`);
    },
    [navigate],
  );

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 2 }}>
        Error Groups
      </Typography>

      <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel>Severity</InputLabel>
          <Select
            value={severity}
            label="Severity"
            onChange={(e) => {
              setSeverity(e.target.value as ErrorSeverity | '');
              setPaginationModel((m) => ({ ...m, page: 0 }));
            }}
          >
            <MenuItem value="">All</MenuItem>
            <MenuItem value="Critical">Critical</MenuItem>
            <MenuItem value="Warning">Warning</MenuItem>
            <MenuItem value="Info">Info</MenuItem>
          </Select>
        </FormControl>

        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel>Status</InputLabel>
          <Select
            value={status}
            label="Status"
            onChange={(e) => {
              setStatus(e.target.value as ErrorStatus | '');
              setPaginationModel((m) => ({ ...m, page: 0 }));
            }}
          >
            <MenuItem value="">All</MenuItem>
            <MenuItem value="Active">Active</MenuItem>
            <MenuItem value="Resolved">Resolved</MenuItem>
            <MenuItem value="Ignored">Ignored</MenuItem>
            <MenuItem value="Expected">Expected</MenuItem>
          </Select>
        </FormControl>

        <FormControl size="small" sx={{ minWidth: 180 }}>
          <InputLabel>Data Source</InputLabel>
          <Select
            value={dataSourceId}
            label="Data Source"
            onChange={(e) => {
              setDataSourceId(e.target.value);
              setPaginationModel((m) => ({ ...m, page: 0 }));
            }}
          >
            <MenuItem value="">All</MenuItem>
            {dataSources?.map((ds) => (
              <MenuItem key={ds.id} value={ds.id}>
                {ds.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Stack>

      <DataGrid
        rows={data?.items ?? []}
        columns={columns}
        loading={isLoading}
        rowCount={data?.totalCount ?? 0}
        paginationMode="server"
        paginationModel={paginationModel}
        onPaginationModelChange={setPaginationModel}
        pageSizeOptions={[10, 25, 50]}
        onRowClick={handleRowClick}
        disableRowSelectionOnClick
        autoHeight
        sx={{
          cursor: 'pointer',
          '& .MuiDataGrid-row:hover': { backgroundColor: 'action.hover' },
        }}
      />
    </Box>
  );
}
