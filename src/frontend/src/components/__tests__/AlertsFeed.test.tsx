import { screen } from '@testing-library/react';
import { renderWithProviders } from '../../test/test-utils';
import AlertsFeed from '../AlertsFeed';

const mockAlerts = {
  items: [
    {
      id: 'a-1',
      knownErrorId: 'ke-1',
      knownErrorMessage: 'High error rate detected',
      status: 'Firing' as const,
      thresholdType: 'Absolute' as const,
      thresholdValue: 10,
      actualValue: 25,
      notificationCount: 3,
      lastNotifiedAt: '2025-01-01T01:00:00Z',
      acknowledgedAt: null,
      resolvedAt: null,
      createdAt: '2025-01-01T00:00:00Z',
    },
    {
      id: 'a-2',
      knownErrorId: 'ke-2',
      knownErrorMessage: 'Latency spike',
      status: 'Firing' as const,
      thresholdType: 'PercentageIncrease' as const,
      thresholdValue: 50,
      actualValue: 55,
      notificationCount: 1,
      lastNotifiedAt: null,
      acknowledgedAt: null,
      resolvedAt: null,
      createdAt: '2025-01-01T00:30:00Z',
    },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 50,
};

const mockCorrelated = [
  {
    id: 'cs-1',
    dataSourceId: 'ds-1',
    dataSourceName: 'Prod',
    status: 'Firing' as const,
    alertIds: 'a-1,a-2',
    groupCount: 2,
    detectedAt: '2025-01-01T00:00:00Z',
    resolvedAt: null,
    createdAt: '2025-01-01T00:00:00Z',
  },
];

vi.mock('../../api/hooks/useAlerts', () => ({
  useAlerts: () => ({
    data: mockAlerts,
    isLoading: false,
    error: null,
  }),
  useCorrelatedAlerts: () => ({
    data: mockCorrelated,
    isLoading: false,
  }),
  useAcknowledgeAlert: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
}));

describe('AlertsFeed', () => {
  it('renders the Active Alerts heading', () => {
    renderWithProviders(<AlertsFeed />);
    expect(screen.getByText('Active Alerts')).toBeInTheDocument();
  });

  it('renders alert messages sorted by severity', () => {
    renderWithProviders(<AlertsFeed />);
    expect(screen.getByText('High error rate detected')).toBeInTheDocument();
    expect(screen.getByText('Latency spike')).toBeInTheDocument();
  });

  it('shows correlated spike count', () => {
    renderWithProviders(<AlertsFeed />);
    expect(screen.getByText('(1 correlated spike)')).toBeInTheDocument();
  });
});

describe('AlertsFeed - empty state', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('shows empty state message when no alerts', async () => {
    vi.doMock('../../api/hooks/useAlerts', () => ({
      useAlerts: () => ({
        data: { items: [], totalCount: 0, page: 1, pageSize: 50 },
        isLoading: false,
        error: null,
      }),
      useCorrelatedAlerts: () => ({ data: [], isLoading: false }),
      useAcknowledgeAlert: () => ({ mutate: vi.fn(), isPending: false }),
    }));

    const { default: AlertsFeedEmpty } = await import('../AlertsFeed');
    renderWithProviders(<AlertsFeedEmpty />);
    expect(screen.getByText('No active alerts. All clear.')).toBeInTheDocument();
  });
});
