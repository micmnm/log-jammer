import { screen } from '@testing-library/react';
import { renderWithProviders, userEvent } from '../../test/test-utils';
import DataSources from '../DataSources';

const mockDataSources = [
  {
    id: 'ds-1',
    name: 'Production ES',
    adapterType: 'Elasticsearch' as const,
    connectionConfig: '{"url":"http://localhost:9200"}',
    pollIntervalSeconds: 30,
    schemaMapping: null,
    samplingBudget: 500,
    enabled: true,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
    fingerprintConfigs: [],
  },
  {
    id: 'ds-2',
    name: 'App Logs',
    adapterType: 'LogFile' as const,
    connectionConfig: '{"filePath":"/var/log/app.log"}',
    pollIntervalSeconds: 60,
    schemaMapping: null,
    samplingBudget: 200,
    enabled: false,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
    fingerprintConfigs: [],
  },
];

const mockDeletionImpact = {
  errorGroupCount: 5,
  occurrenceCount: 120,
  alertCount: 3,
  classificationQueueCount: 2,
  tagCount: 8,
  ruleCount: 1,
};

const mockDeleteMutate = vi.fn();
const mockUpdateMutate = vi.fn();
const mockTestMutate = vi.fn();

vi.mock('../../api/hooks/useDataSources', () => ({
  useDataSources: () => ({
    data: mockDataSources,
    isLoading: false,
  }),
  useDeleteDataSource: () => ({
    mutate: mockDeleteMutate,
  }),
  useUpdateDataSource: () => ({
    mutate: mockUpdateMutate,
  }),
  useTestConnection: () => ({
    mutate: mockTestMutate,
    isPending: false,
  }),
  useCreateDataSource: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
  useDataSourceSchema: () => ({
    data: null,
    isLoading: false,
  }),
  useSampleRecords: () => ({
    data: null,
    isLoading: false,
  }),
  useDetectLogFile: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
  useDeletionImpact: () => ({
    data: mockDeletionImpact,
    isLoading: false,
  }),
}));

vi.mock('../../api/hooks/useFingerprintConfigs', () => ({
  useFingerprintConfigs: () => ({
    data: [],
    isLoading: false,
  }),
  useCreateFingerprintConfig: () => ({
    mutateAsync: vi.fn(),
  }),
  useDeleteFingerprintConfig: () => ({
    mutateAsync: vi.fn(),
  }),
}));

describe('DataSources', () => {
  beforeEach(() => {
    mockDeleteMutate.mockClear();
    mockUpdateMutate.mockClear();
    mockTestMutate.mockClear();
  });

  it('renders the page title', () => {
    renderWithProviders(<DataSources />);
    expect(screen.getByText('Data Sources')).toBeInTheDocument();
  });

  it('renders data source table with rows', () => {
    renderWithProviders(<DataSources />);
    expect(screen.getByText('Production ES')).toBeInTheDocument();
    expect(screen.getByText('App Logs')).toBeInTheDocument();
  });

  it('renders adapter types', () => {
    renderWithProviders(<DataSources />);
    expect(screen.getByText('Elasticsearch')).toBeInTheDocument();
    expect(screen.getByText('LogFile')).toBeInTheDocument();
  });

  it('renders poll intervals', () => {
    renderWithProviders(<DataSources />);
    expect(screen.getByText('30s')).toBeInTheDocument();
    expect(screen.getByText('60s')).toBeInTheDocument();
  });

  it('shows add button', () => {
    renderWithProviders(<DataSources />);
    expect(screen.getByText('Add Data Source')).toBeInTheDocument();
  });

  it('opens add dialog when Add button is clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<DataSources />);
    await user.click(screen.getByText('Add Data Source'));
    expect(screen.getByText('Add Data Source', { selector: 'h2' })).toBeInTheDocument();
  });

  it('shows deletion impact in delete dialog', async () => {
    const user = userEvent.setup();
    renderWithProviders(<DataSources />);
    const deleteButtons = screen.getAllByTitle('Delete');
    await user.click(deleteButtons[0]);
    expect(screen.getByText('Delete Data Source')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getAllByText(/error group/).length).toBeGreaterThan(0);
    expect(screen.getByText('120')).toBeInTheDocument();
    expect(screen.getAllByText(/occurrence/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/alert/).length).toBeGreaterThan(0);
  });

  it('shows preserve history checkbox in delete dialog', async () => {
    const user = userEvent.setup();
    renderWithProviders(<DataSources />);
    const deleteButtons = screen.getAllByTitle('Delete');
    await user.click(deleteButtons[0]);
    expect(screen.getByLabelText(/Keep historical error groups/)).toBeInTheDocument();
  });

  it('calls delete without preserveHistory by default', async () => {
    const user = userEvent.setup();
    renderWithProviders(<DataSources />);
    const deleteButtons = screen.getAllByTitle('Delete');
    await user.click(deleteButtons[0]);
    await user.click(screen.getByRole('button', { name: 'Delete' }));
    expect(mockDeleteMutate).toHaveBeenCalledWith({ id: 'ds-1', preserveHistory: false });
  });

  it('calls delete with preserveHistory when checkbox is checked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<DataSources />);
    const deleteButtons = screen.getAllByTitle('Delete');
    await user.click(deleteButtons[0]);
    await user.click(screen.getByLabelText(/Keep historical error groups/));
    await user.click(screen.getByRole('button', { name: 'Delete' }));
    expect(mockDeleteMutate).toHaveBeenCalledWith({ id: 'ds-1', preserveHistory: true });
  });
});
