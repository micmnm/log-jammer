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
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  CircularProgress,
} from '@mui/material';
import type { DataSourceResponse, AdapterType, DetectResponse, DiscoverIndicesResponse, SchemaResponse } from '../api/types';
import { useCreateDataSource, useUpdateDataSource, useTestConnection, useDetectLogFile, useDiscoverIndices, useDiscoverSchema } from '../api/hooks/useDataSources';

interface ElasticsearchConfig {
  url: string;
  indexPattern: string;
  auth?: {
    type: string;
    username?: string;
    password?: string;
  };
}

interface PostgreSqlConfig {
  connectionString: string;
  table: string;
  timestampColumn: string;
}

interface LogFileConfig {
  filePath: string;
  parseMode: string;
  timestampField: string;
  levelField: string;
  messageField: string;
  regexPattern: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  dataSource: DataSourceResponse | null;
}

function parseConfig(configJson: string | undefined): Record<string, unknown> {
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
  const detectLogFile = useDetectLogFile();
  const discoverIndices = useDiscoverIndices();
  const discoverSchema = useDiscoverSchema();

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
  const [lfTimestampField, setLfTimestampField] = useState('');
  const [lfLevelField, setLfLevelField] = useState('');
  const [lfMessageField, setLfMessageField] = useState('');
  const [lfRegexPattern, setLfRegexPattern] = useState('');
  const [lfDetected, setLfDetected] = useState(false);
  const [lfDetectResult, setLfDetectResult] = useState<DetectResponse | null>(null);

  // ES discovery state
  const [discoveredIndices, setDiscoveredIndices] = useState<DiscoverIndicesResponse | null>(null);
  const [showConcreteIndices, setShowConcreteIndices] = useState(false);
  const [discoveredSchema, setDiscoveredSchema] = useState<SchemaResponse | null>(null);
  const [discoverError, setDiscoverError] = useState<string | null>(null);

  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [detectError, setDetectError] = useState<string | null>(null);

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
          setEsUrl(String(config.url ?? ''));
          setEsIndexPattern(String(config.indexPattern ?? ''));
          const auth = config.auth as { username?: string; password?: string } | undefined;
          setEsUsername(auth?.username ?? '');
          setEsPassword(auth?.password ?? '');
        } else if (dataSource.adapterType === 'PostgreSql') {
          setPgConnectionString(String(config.connectionString ?? ''));
          setPgTable(String(config.table ?? ''));
          setPgTimestampColumn(String(config.timestampColumn ?? ''));
        } else if (dataSource.adapterType === 'LogFile') {
          setLfFilePath(String(config.filePath ?? ''));
          setLfParseMode(String(config.parseMode ?? ''));
          setLfTimestampField(String(config.timestampField ?? ''));
          setLfLevelField(String(config.levelField ?? ''));
          setLfMessageField(String(config.messageField ?? ''));
          setLfRegexPattern(String(config.regexPattern ?? ''));
          setLfDetected(true); // existing data source already has config
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
        setLfTimestampField('');
        setLfLevelField('');
        setLfMessageField('');
        setLfRegexPattern('');
        setLfDetected(false);
        setLfDetectResult(null);
      }
      setTestResult(null);
      setDetectError(null);
      setDiscoveredIndices(null);
      setDiscoveredSchema(null);
      setDiscoverError(null);
      setShowConcreteIndices(false);
    }
  }, [open, dataSource]);

  const buildConnectionConfig = (): string => {
    if (adapterType === 'Elasticsearch') {
      const config: ElasticsearchConfig = { url: esUrl, indexPattern: esIndexPattern };
      if (esUsername || esPassword) {
        config.auth = { type: 'basic', username: esUsername, password: esPassword };
      }
      return JSON.stringify(config);
    } else if (adapterType === 'PostgreSql') {
      return JSON.stringify({ connectionString: pgConnectionString, table: pgTable, timestampColumn: pgTimestampColumn } satisfies PostgreSqlConfig);
    } else {
      return JSON.stringify({
        filePath: lfFilePath,
        parseMode: lfParseMode,
        timestampField: lfTimestampField,
        levelField: lfLevelField,
        messageField: lfMessageField,
        regexPattern: lfRegexPattern,
      } satisfies LogFileConfig);
    }
  };

  const isLogFileMandatoryFilled =
    lfFilePath && lfDetected && lfTimestampField && lfLevelField && lfMessageField && lfParseMode &&
    (lfParseMode !== 'regex' || lfRegexPattern);

  const canSave = adapterType === 'LogFile'
    ? !!(name && isLogFileMandatoryFilled)
    : !!name;

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

  const buildEsConnectionConfig = (indexPattern?: string): string => {
    const config: ElasticsearchConfig = { url: esUrl, indexPattern: indexPattern ?? esIndexPattern };
    if (esUsername || esPassword) {
      config.auth = { type: 'basic', username: esUsername, password: esPassword };
    }
    return JSON.stringify(config);
  };

  const handleDiscoverIndices = () => {
    if (!esUrl) return;
    setDiscoverError(null);
    setDiscoveredIndices(null);
    discoverIndices.mutate(
      { connectionConfig: buildEsConnectionConfig('*'), showConcreteIndices: showConcreteIndices },
      {
        onSuccess: (result) => setDiscoveredIndices(result),
        onError: (err) => setDiscoverError(String(err)),
      },
    );
  };

  const handleDiscoverSchema = () => {
    if (!esIndexPattern) return;
    setDiscoverError(null);
    setDiscoveredSchema(null);
    discoverSchema.mutate(
      { connectionConfig: buildEsConnectionConfig() },
      {
        onSuccess: (result) => setDiscoveredSchema(result),
        onError: (err) => setDiscoverError(String(err)),
      },
    );
  };

  const handleDetect = () => {
    if (!lfFilePath) return;
    setDetectError(null);
    setLfDetectResult(null);
    detectLogFile.mutate(lfFilePath, {
      onSuccess: (result: DetectResponse) => {
        setLfDetected(true);
        setLfParseMode(result.proposedConfig.parseMode);
        setLfTimestampField(result.proposedConfig.timestampField ?? '');
        setLfLevelField(result.proposedConfig.levelField ?? '');
        setLfMessageField(result.proposedConfig.messageField ?? '');
        setLfRegexPattern(result.proposedConfig.regexPattern ?? '');
        setLfDetectResult(result);
      },
      onError: (err) => {
        setDetectError(String(err));
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
            <TextField label="URL" value={esUrl} onChange={(e) => { setEsUrl(e.target.value); setDiscoveredIndices(null); setDiscoveredSchema(null); setDiscoverError(null); }} fullWidth margin="dense" />
            <TextField label="Index Pattern" value={esIndexPattern} onChange={(e) => { setEsIndexPattern(e.target.value); setDiscoveredSchema(null); }} fullWidth margin="dense" />
            <TextField label="Username" value={esUsername} onChange={(e) => setEsUsername(e.target.value)} fullWidth margin="dense" />
            <TextField label="Password" type="password" value={esPassword} onChange={(e) => setEsPassword(e.target.value)} fullWidth margin="dense" />

            {/* Discover Indices */}
            <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mt: 1 }}>
              <Button
                variant="outlined"
                onClick={handleDiscoverIndices}
                disabled={!esUrl || discoverIndices.isPending}
                sx={{ minWidth: 150 }}
              >
                {discoverIndices.isPending ? <CircularProgress size={20} /> : 'Discover Indices'}
              </Button>
              <FormControlLabel
                control={<Switch size="small" checked={showConcreteIndices} onChange={(e) => setShowConcreteIndices(e.target.checked)} />}
                label="Show concrete indices"
                slotProps={{ typography: { variant: 'body2' } }}
              />
            </Box>

            {discoverError && (
              <Alert severity="error" sx={{ mt: 1 }}>{discoverError}</Alert>
            )}

            {discoveredIndices && (
              <Box sx={{ mt: 1 }}>
                {discoveredIndices.aliases.length > 0 && (
                  <Box sx={{ mb: 1 }}>
                    <Typography variant="subtitle2" gutterBottom>Aliases</Typography>
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                      {discoveredIndices.aliases.map((a) => (
                        <Chip
                          key={a.name}
                          label={`${a.name} (${a.indices.length})`}
                          size="small"
                          color={esIndexPattern === a.name ? 'primary' : 'default'}
                          variant={esIndexPattern === a.name ? 'filled' : 'outlined'}
                          onClick={() => setEsIndexPattern(a.name)}
                        />
                      ))}
                    </Box>
                  </Box>
                )}
                {discoveredIndices.dataStreams.length > 0 && (
                  <Box sx={{ mb: 1 }}>
                    <Typography variant="subtitle2" gutterBottom>Data Streams</Typography>
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                      {discoveredIndices.dataStreams.map((ds) => (
                        <Chip
                          key={ds.name}
                          label={`${ds.name} (${ds.backingIndices})`}
                          size="small"
                          color={esIndexPattern === ds.name ? 'primary' : 'default'}
                          variant={esIndexPattern === ds.name ? 'filled' : 'outlined'}
                          onClick={() => setEsIndexPattern(ds.name)}
                        />
                      ))}
                    </Box>
                  </Box>
                )}
                {showConcreteIndices && discoveredIndices.concreteIndices.length > 0 && (
                  <Box sx={{ mb: 1 }}>
                    <Typography variant="subtitle2" gutterBottom>Concrete Indices</Typography>
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                      {discoveredIndices.concreteIndices.map((idx) => (
                        <Chip
                          key={idx}
                          label={idx}
                          size="small"
                          color={esIndexPattern === idx ? 'primary' : 'default'}
                          variant={esIndexPattern === idx ? 'filled' : 'outlined'}
                          onClick={() => setEsIndexPattern(idx)}
                        />
                      ))}
                    </Box>
                  </Box>
                )}
                {discoveredIndices.aliases.length === 0 && discoveredIndices.dataStreams.length === 0 && discoveredIndices.concreteIndices.length === 0 && (
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>No indices found.</Typography>
                )}
              </Box>
            )}

            {/* View Schema */}
            <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mt: 1 }}>
              <Button
                variant="outlined"
                onClick={handleDiscoverSchema}
                disabled={!esIndexPattern || discoverSchema.isPending}
                sx={{ minWidth: 130 }}
              >
                {discoverSchema.isPending ? <CircularProgress size={20} /> : 'View Schema'}
              </Button>
            </Box>

            {discoveredSchema && discoveredSchema.fields.length > 0 && (
              <Box sx={{ mt: 1 }}>
                <Typography variant="subtitle2" gutterBottom>
                  Fields ({discoveredSchema.fields.length})
                </Typography>
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                  {discoveredSchema.fields.map((f) => (
                    <Chip
                      key={f.name}
                      label={`${f.name}: ${f.type}`}
                      size="small"
                      variant="outlined"
                    />
                  ))}
                </Box>
              </Box>
            )}
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
            <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
              <TextField
                label="File Path"
                value={lfFilePath}
                onChange={(e) => { setLfFilePath(e.target.value); setLfDetected(false); }}
                fullWidth
                margin="dense"
                required
              />
              <Button
                variant="outlined"
                onClick={handleDetect}
                disabled={!lfFilePath || detectLogFile.isPending}
                sx={{ mt: 1, minWidth: 90 }}
              >
                {detectLogFile.isPending ? <CircularProgress size={20} /> : 'Detect'}
              </Button>
            </Box>

            {detectError && (
              <Alert severity="error" sx={{ mt: 1 }}>{detectError}</Alert>
            )}

            {lfDetected && (
              <>
                <FormControl fullWidth margin="dense">
                  <InputLabel>Parse Mode</InputLabel>
                  <Select
                    value={lfParseMode}
                    label="Parse Mode"
                    onChange={(e) => setLfParseMode(e.target.value)}
                  >
                    <MenuItem value="jsonlines">JSON Lines</MenuItem>
                    <MenuItem value="regex">Regex</MenuItem>
                  </Select>
                </FormControl>

                <TextField
                  label="Timestamp Field"
                  value={lfTimestampField}
                  onChange={(e) => setLfTimestampField(e.target.value)}
                  fullWidth
                  margin="dense"
                  required
                />
                <TextField
                  label="Level Field"
                  value={lfLevelField}
                  onChange={(e) => setLfLevelField(e.target.value)}
                  fullWidth
                  margin="dense"
                  required
                />
                <TextField
                  label="Message Field"
                  value={lfMessageField}
                  onChange={(e) => setLfMessageField(e.target.value)}
                  fullWidth
                  margin="dense"
                  required
                />

                {lfParseMode === 'regex' && (
                  <TextField
                    label="Regex Pattern"
                    value={lfRegexPattern}
                    onChange={(e) => setLfRegexPattern(e.target.value)}
                    fullWidth
                    margin="dense"
                    required
                  />
                )}
              </>
            )}

            {lfDetectResult && lfDetectResult.fields.length > 0 && (
              <Box sx={{ mt: 2 }}>
                <Typography variant="subtitle2" gutterBottom>
                  Detected Fields ({lfDetectResult.detectedFormat})
                </Typography>
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mb: 1 }}>
                  {lfDetectResult.fields.map((f) => (
                    <Chip
                      key={f.name}
                      label={f.name}
                      size="small"
                      color={f.proposedRole ? 'primary' : 'default'}
                      variant={f.proposedRole ? 'filled' : 'outlined'}
                    />
                  ))}
                </Box>
              </Box>
            )}

            {lfDetectResult && lfDetectResult.sampleRecords.length > 0 && (
              <Box sx={{ mt: 1 }}>
                <Typography variant="subtitle2" gutterBottom>
                  Sample Records
                </Typography>
                <TableContainer sx={{ maxHeight: 200 }}>
                  <Table size="small" stickyHeader>
                    <TableHead>
                      <TableRow>
                        {lfDetectResult.fields.map((f) => (
                          <TableCell key={f.name} sx={{ fontFamily: (theme) => theme.fontFamilyMono, fontSize: '0.75rem' }}>
                            {f.name}
                          </TableCell>
                        ))}
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {lfDetectResult.sampleRecords.map((record, i) => (
                        <TableRow key={i}>
                          {lfDetectResult!.fields.map((f) => (
                            <TableCell key={f.name} sx={{ fontFamily: (theme) => theme.fontFamilyMono, fontSize: '0.7rem', maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                              {String(record[f.name] ?? '')}
                            </TableCell>
                          ))}
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Box>
            )}
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
        <Button onClick={handleSave} variant="contained" disabled={!canSave}>
          {isEdit ? 'Save' : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
