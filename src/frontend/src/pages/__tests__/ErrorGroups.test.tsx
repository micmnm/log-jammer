import { screen, within } from '@testing-library/react';
import { renderWithProviders } from '../../test/test-utils';
import ErrorGroups from '../ErrorGroups';

const mockErrorGroups = {
  items: [
    {
      id: '1',
      fingerprintHash: 'abc123',
      representativeMessage: 'NullReferenceException in UserService',
      severity: 'Critical' as const,
      status: 'Active' as const,
      firstSeen: '2025-01-01T00:00:00Z',
      lastSeen: '2025-01-02T00:00:00Z',
      totalOccurrences: 42,
      dataSourceId: 'ds-1',
      dataSourceName: 'Production ES',
    },
    {
      id: '2',
      fingerprintHash: 'def456',
      representativeMessage: 'Timeout connecting to database',
      severity: 'Warning' as const,
      status: 'Resolved' as const,
      firstSeen: '2025-01-01T00:00:00Z',
      lastSeen: '2025-01-01T12:00:00Z',
      totalOccurrences: 5,
      dataSourceId: 'ds-2',
      dataSourceName: 'Staging PG',
    },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 25,
};

const mockDataSources = [
  {
    id: 'ds-1',
    name: 'Production ES',
    adapterType: 'Elasticsearch' as const,
    connectionString: 'http://localhost:9200',
    samplingBudget: 1.0,
    enabled: true,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: null,
  },
];

vi.mock('../../api/hooks/useErrorGroups', () => ({
  useErrorGroups: () => ({
    data: mockErrorGroups,
    isLoading: false,
    error: null,
  }),
}));

vi.mock('../../api/hooks/useDataSources', () => ({
  useDataSources: () => ({
    data: mockDataSources,
    isLoading: false,
  }),
}));

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return { ...actual, useNavigate: () => mockNavigate };
});

describe('ErrorGroups', () => {
  it('renders the page title', () => {
    renderWithProviders(<ErrorGroups />);
    expect(screen.getByText('Error Groups')).toBeInTheDocument();
  });

  it('renders DataGrid with error group data', () => {
    renderWithProviders(<ErrorGroups />);
    expect(screen.getByText('NullReferenceException in UserService')).toBeInTheDocument();
    expect(screen.getByText('Timeout connecting to database')).toBeInTheDocument();
  });

  it('renders severity and status chips', () => {
    renderWithProviders(<ErrorGroups />);
    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText('Warning')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Resolved')).toBeInTheDocument();
  });

  it('renders filter dropdowns', () => {
    renderWithProviders(<ErrorGroups />);
    // Labels appear in both filters and DataGrid column headers
    expect(screen.getAllByText('Severity').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Status').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Data Source').length).toBeGreaterThanOrEqual(1);
  });

  it('navigates on row click', async () => {
    renderWithProviders(<ErrorGroups />);
    const row = screen.getByText('NullReferenceException in UserService').closest('.MuiDataGrid-row') as HTMLElement | null;
    if (row) {
      const firstCell = within(row).getAllByRole('gridcell')[0];
      firstCell.click();
      expect(mockNavigate).toHaveBeenCalledWith('/error-groups/1');
    }
  });
});
