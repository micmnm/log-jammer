import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Switch,
  FormControlLabel,
  Alert,
  Box,
} from '@mui/material';
import type { DataSourceResponse, AdapterType } from '../api/types';
import { useCreateDataSource, useUpdateDataSource, useTestConnection } from '../api/hooks/useDataSources';

interface ElasticsearchConfig {
  url: string;
  indexPattern: string;
  username: string;
  password: string;
}

interface PostgreSqlConfig {
  connectionString: string;
  table: string;
  timestampColumn: string;
}

interface LogFileConfig {
  filePath: string;
  parseMode: string;
  regexPattern: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  dataSource: DataSourceResponse | null;
}

function parseConfig(configJson: string | undefined): Record<string, string> {
  if (!configJson) return {};
  try {
    return JSON.parse(configJson);
  } catch {
    return {};
  }
}

export default function DataSourceDialog({ open, onClose, dataSource }: Props) {
  const createDataSource = useCreateDataSource();
  const updateDataSource = useUpdateDataSource();
  const testConnection = useTestConnection();

  const isEdit = !!dataSource;

  const [name, setName] = useState('');
  const [adapterType, setAdapterType] = useState<AdapterType>('Elasticsearch');
  const [enabled, setEnabled] = useState(true);
  const [pollIntervalSeconds, setPollIntervalSeconds] = useState(30);
  const [samplingBudget, setSamplingBudget] = useState(500);

  // Elasticsearch fields
  const [esUrl, setEsUrl] = useState('');
  const [esIndexPattern, setEsIndexPattern] = useState('');
  const [esUsername, setEsUsername] = useState('');
  const [esPassword, setEsPassword] = useState('');

  // PostgreSql fields
  const [pgConnectionString, setPgConnectionString] = useState('');
  const [pgTable, setPgTable] = useState('');
  const [pgTimestampColumn, setPgTimestampColumn] = useState('');

  // LogFile fields
  const [lfFilePath, setLfFilePath] = useState('');
  const [lfParseMode, setLfParseMode] = useState('');
  const [lfRegexPattern, setLfRegexPattern] = useState('');

  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  useEffect(() => {
    if (open) {
      if (dataSource) {
        setName(dataSource.name);
        setAdapterType(dataSource.adapterType);
        setEnabled(dataSource.enabled);
        setPollIntervalSeconds(dataSource.pollIntervalSeconds);
        setSamplingBudget(dataSource.samplingBudget);

        const config = parseConfig(dataSource.connectionConfig);
        if (dataSource.adapterType === 'Elasticsearch') {
          setEsUrl(config.url ?? '');
          setEsIndexPattern(config.indexPattern ?? '');
          setEsUsername(config.username ?? '');
          setEsPassword(config.password ?? '');
        } else if (dataSource.adapterType === 'PostgreSql') {
          setPgConnectionString(config.connectionString ?? '');
          setPgTable(config.table ?? '');
          setPgTimestampColumn(config.timestampColumn ?? '');
        } else if (dataSource.adapterType === 'LogFile') {
          setLfFilePath(config.filePath ?? '');
          setLfParseMode(config.parseMode ?? '');
          setLfRegexPattern(config.regexPattern ?? '');
        }
      } else {
        setName('');
        setAdapterType('Elasticsearch');
        setEnabled(true);
        setPollIntervalSeconds(30);
        setSamplingBudget(500);
        setEsUrl('');
        setEsIndexPattern('');
        setEsUsername('');
        setEsPassword('');
        setPgConnectionString('');
        setPgTable('');
        setPgTimestampColumn('');
        setLfFilePath('');
        setLfParseMode('');
        setLfRegexPattern('');
      }
      setTestResult(null);
    }
  }, [open, dataSource]);

  const buildConnectionConfig = (): string => {
    if (adapterType === 'Elasticsearch') {
      return JSON.stringify({ url: esUrl, indexPattern: esIndexPattern, username: esUsername, password: esPassword } satisfies ElasticsearchConfig);
    } else if (adapterType === 'PostgreSql') {
      return JSON.stringify({ connectionString: pgConnectionString, table: pgTable, timestampColumn: pgTimestampColumn } satisfies PostgreSqlConfig);
    } else {
      return JSON.stringify({ filePath: lfFilePath, parseMode: lfParseMode, regexPattern: lfRegexPattern } satisfies LogFileConfig);
    }
  };

  const handleSave = () => {
    const connectionConfig = buildConnectionConfig();
    if (isEdit) {
      updateDataSource.mutate(
        { id: dataSource.id, request: { name, adapterType, connectionConfig, enabled, pollIntervalSeconds, samplingBudget } },
        { onSuccess: () => onClose() },
      );
    } else {
      createDataSource.mutate(
        { name, adapterType, connectionConfig, enabled, pollIntervalSeconds, samplingBudget },
        { onSuccess: () => onClose() },
      );
    }
  };

  const handleTestConnection = () => {
    if (!dataSource) return;
    setTestResult(null);
    testConnection.mutate(dataSource.id, {
      onSuccess: (result) => {
        setTestResult({
          success: result.success,
          message: result.success
            ? `Connection successful (${result.latencyMs.toFixed(0)}ms)`
            : `Failed: ${result.errorMessage}`,
        });
      },
      onError: (err) => {
        setTestResult({ success: false, message: String(err) });
      },
    });
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? 'Edit Data Source' : 'Add Data Source'}</DialogTitle>
      <DialogContent>
        <TextField
          label="Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          fullWidth
          margin="normal"
          required
        />
        <FormControl fullWidth margin="normal">
          <InputLabel>Adapter Type</InputLabel>
          <Select
            value={adapterType}
            label="Adapter Type"
            onChange={(e) => setAdapterType(e.target.value as AdapterType)}
          >
            <MenuItem value="Elasticsearch">Elasticsearch</MenuItem>
            <MenuItem value="PostgreSql">PostgreSQL</MenuItem>
            <MenuItem value="LogFile">Log File</MenuItem>
          </Select>
        </FormControl>
        <FormControlLabel
          control={<Switch checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />}
          label="Enabled"
          sx={{ mt: 1 }}
        />

        {/* Adapter-specific sections */}
        {adapterType === 'Elasticsearch' && (
          <Box sx={{ mt: 2 }}>
            <TextField label="URL" value={esUrl} onChange={(e) => setEsUrl(e.target.value)} fullWidth margin="dense" />
            <TextField label="Index Pattern" value={esIndexPattern} onChange={(e) => setEsIndexPattern(e.target.value)} fullWidth margin="dense" />
            <TextField label="Username" value={esUsername} onChange={(e) => setEsUsername(e.target.value)} fullWidth margin="dense" />
            <TextField label="Password" type="password" value={esPassword} onChange={(e) => setEsPassword(e.target.value)} fullWidth margin="dense" />
          </Box>
        )}

        {adapterType === 'PostgreSql' && (
          <Box sx={{ mt: 2 }}>
            <TextField label="Connection String" value={pgConnectionString} onChange={(e) => setPgConnectionString(e.target.value)} fullWidth margin="dense" />
            <TextField label="Table" value={pgTable} onChange={(e) => setPgTable(e.target.value)} fullWidth margin="dense" />
            <TextField label="Timestamp Column" value={pgTimestampColumn} onChange={(e) => setPgTimestampColumn(e.target.value)} fullWidth margin="dense" />
          </Box>
        )}

        {adapterType === 'LogFile' && (
          <Box sx={{ mt: 2 }}>
            <TextField label="File Path" value={lfFilePath} onChange={(e) => setLfFilePath(e.target.value)} fullWidth margin="dense" />
            <TextField label="Parse Mode" value={lfParseMode} onChange={(e) => setLfParseMode(e.target.value)} fullWidth margin="dense" />
            <TextField label="Regex Pattern" value={lfRegexPattern} onChange={(e) => setLfRegexPattern(e.target.value)} fullWidth margin="dense" />
          </Box>
        )}

        <TextField
          label="Poll Interval (seconds)"
          type="number"
          value={pollIntervalSeconds}
          onChange={(e) => setPollIntervalSeconds(Number(e.target.value))}
          fullWidth
          margin="normal"
          slotProps={{ htmlInput: { min: 5, max: 86400 } }}
        />
        <TextField
          label="Sampling Budget"
          type="number"
          value={samplingBudget}
          onChange={(e) => setSamplingBudget(Number(e.target.value))}
          fullWidth
          margin="normal"
          slotProps={{ htmlInput: { min: 1, max: 10000 } }}
        />

        {testResult && (
          <Alert severity={testResult.success ? 'success' : 'error'} sx={{ mt: 2 }}>
            {testResult.message}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        {isEdit && (
          <Button onClick={handleTestConnection} disabled={testConnection.isPending}>
            Test Connection
          </Button>
        )}
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSave} variant="contained" disabled={!name}>
          {isEdit ? 'Save' : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
