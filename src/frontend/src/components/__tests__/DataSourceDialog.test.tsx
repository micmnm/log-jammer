import { screen } from '@testing-library/react';
import { renderWithProviders, userEvent } from '../../test/test-utils';
import DataSourceDialog from '../DataSourceDialog';

const mockCreateMutate = vi.fn();
const mockUpdateMutate = vi.fn();
const mockTestMutate = vi.fn();
const mockDetectMutate = vi.fn();

vi.mock('../../api/hooks/useDataSources', () => ({
  useCreateDataSource: () => ({
    mutate: mockCreateMutate,
    isPending: false,
  }),
  useUpdateDataSource: () => ({
    mutate: mockUpdateMutate,
    isPending: false,
  }),
  useTestConnection: () => ({
    mutate: mockTestMutate,
    isPending: false,
  }),
  useDetectLogFile: () => ({
    mutate: mockDetectMutate,
    isPending: false,
  }),
}));

describe('DataSourceDialog', () => {
  beforeEach(() => {
    mockCreateMutate.mockClear();
    mockUpdateMutate.mockClear();
    mockTestMutate.mockClear();
    mockDetectMutate.mockClear();
  });

  it('renders create dialog', () => {
    renderWithProviders(
      <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
    );
    expect(screen.getByText('Add Data Source')).toBeInTheDocument();
    expect(screen.getByLabelText(/Name/)).toBeInTheDocument();
  });

  it('renders Elasticsearch fields by default', () => {
    renderWithProviders(
      <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
    );
    expect(screen.getByLabelText('URL')).toBeInTheDocument();
    expect(screen.getByLabelText('Index Pattern')).toBeInTheDocument();
    expect(screen.getByLabelText('Username')).toBeInTheDocument();
  });

  it('shows PostgreSQL fields when adapter type changed', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
    );
    // Open the select
    const adapterSelect = screen.getAllByText('Elasticsearch')[0];
    await user.click(adapterSelect);
    await user.click(screen.getByText('PostgreSQL'));
    expect(screen.getByLabelText('Connection String')).toBeInTheDocument();
    expect(screen.getByLabelText('Table')).toBeInTheDocument();
    expect(screen.getByLabelText('Timestamp Column')).toBeInTheDocument();
  });

  it('shows Create button for new data source', () => {
    renderWithProviders(
      <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
    );
    expect(screen.getByRole('button', { name: 'Create' })).toBeInTheDocument();
  });

  it('renders edit dialog with existing data', () => {
    const ds = {
      id: 'ds-1',
      name: 'Test ES',
      adapterType: 'Elasticsearch' as const,
      connectionConfig: '{"url":"http://localhost:9200","indexPattern":"logs-*","username":"admin","password":"pass"}',
      pollIntervalSeconds: 30,
      schemaMapping: null,
      samplingBudget: 500,
      enabled: true,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
      lastIngestAt: null,
      fingerprintConfigs: [],
    };
    renderWithProviders(
      <DataSourceDialog open={true} onClose={vi.fn()} dataSource={ds} />,
    );
    expect(screen.getByText('Edit Data Source')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Test Connection' })).toBeInTheDocument();
  });

  it('shows poll interval and sampling budget fields', () => {
    renderWithProviders(
      <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
    );
    expect(screen.getByLabelText('Poll Interval (seconds)')).toBeInTheDocument();
    expect(screen.getByLabelText('Sampling Budget')).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    renderWithProviders(
      <DataSourceDialog open={false} onClose={vi.fn()} dataSource={null} />,
    );
    expect(screen.queryByText('Add Data Source')).not.toBeInTheDocument();
  });

  it('shows LogFile fields with Detect button', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
    );
    const adapterSelect = screen.getAllByText('Elasticsearch')[0];
    await user.click(adapterSelect);
    await user.click(screen.getByText('Log File'));
    expect(screen.getByLabelText(/File Path/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Detect' })).toBeInTheDocument();
  });

  it('disables Create when LogFile mandatory fields are empty', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
    );
    const adapterSelect = screen.getAllByText('Elasticsearch')[0];
    await user.click(adapterSelect);
    await user.click(screen.getByText('Log File'));
    // Name is filled but detect hasn't run
    await user.type(screen.getByLabelText(/Name/), 'Test');
    expect(screen.getByRole('button', { name: 'Create' })).toBeDisabled();
  });

  it('enables Create after detect fills mandatory fields', async () => {
    const user = userEvent.setup();
    mockDetectMutate.mockImplementation((_path: string, opts: { onSuccess: (data: unknown) => void }) => {
      opts.onSuccess({
        detectedFormat: 'jsonlines',
        fields: [
          { name: 'timestamp', type: 'String', proposedRole: 'Timestamp' },
          { name: 'level', type: 'String', proposedRole: 'Level' },
          { name: 'message', type: 'String', proposedRole: 'Message' },
        ],
        sampleRecords: [{ timestamp: '2026-01-01', level: 'ERROR', message: 'test' }],
        proposedConfig: {
          filePath: '/app/logs/test.json',
          parseMode: 'jsonlines',
          timestampField: 'timestamp',
          levelField: 'level',
          messageField: 'message',
          regexPattern: null,
        },
      });
    });

    renderWithProviders(
      <DataSourceDialog open={true} onClose={vi.fn()} dataSource={null} />,
    );
    const adapterSelect = screen.getAllByText('Elasticsearch')[0];
    await user.click(adapterSelect);
    await user.click(screen.getByText('Log File'));
    await user.type(screen.getByLabelText(/Name/), 'Test');
    await user.type(screen.getByLabelText(/File Path/), '/app/logs/test.json');
    await user.click(screen.getByRole('button', { name: 'Detect' }));

    expect(screen.getByRole('button', { name: 'Create' })).not.toBeDisabled();
  });
});
