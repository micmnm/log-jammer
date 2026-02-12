import { screen } from '@testing-library/react';
import { renderWithProviders } from '../../test/test-utils';
import ErrorGroupDetail from '../ErrorGroupDetail';

const mockErrorGroup = {
  id: '1',
  fingerprintHash: 'abc123def456',
  representativeMessage: 'NullReferenceException in UserService',
  severity: 'Critical' as const,
  status: 'Active' as const,
  firstSeen: '2025-01-01T00:00:00Z',
  lastSeen: '2025-01-02T00:00:00Z',
  totalOccurrences: 42,
  dataSourceId: 'ds-1',
  dataSourceName: 'Production ES',
  representativeStackTrace: 'at UserService.GetUser()\nat Controller.Index()',
};

const mockOccurrences = [
  {
    windowStart: '2025-01-01T00:00:00Z',
    windowEnd: '2025-01-01T01:00:00Z',
    count: 10,
    sampleRatio: 1.0,
    extrapolatedCount: 10,
  },
  {
    windowStart: '2025-01-01T01:00:00Z',
    windowEnd: '2025-01-01T02:00:00Z',
    count: 5,
    sampleRatio: 0.5,
    extrapolatedCount: 10,
  },
];

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return { ...actual, useParams: () => ({ id: '1' }), useNavigate: () => vi.fn() };
});

vi.mock('../../api/hooks/useErrorGroups', () => ({
  useErrorGroup: () => ({
    data: mockErrorGroup,
    isLoading: false,
    error: null,
  }),
  useErrorGroupOccurrences: () => ({
    data: mockOccurrences,
    isLoading: false,
  }),
  useUpdateErrorGroupStatus: () => ({ mutate: vi.fn(), isPending: false }),
  useUpdateErrorGroupSeverity: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('../../api/hooks/useAlerts', () => ({
  useAlerts: () => ({ data: { items: [] }, isLoading: false }),
}));

// Mock canvas for chart.js
HTMLCanvasElement.prototype.getContext = vi.fn().mockReturnValue({
  canvas: { width: 300, height: 150 },
  clearRect: vi.fn(),
  beginPath: vi.fn(),
  moveTo: vi.fn(),
  lineTo: vi.fn(),
  stroke: vi.fn(),
  fill: vi.fn(),
  arc: vi.fn(),
  measureText: vi.fn().mockReturnValue({ width: 0 }),
  fillText: vi.fn(),
  fillRect: vi.fn(),
  strokeRect: vi.fn(),
  setTransform: vi.fn(),
  resetTransform: vi.fn(),
  save: vi.fn(),
  restore: vi.fn(),
  scale: vi.fn(),
  translate: vi.fn(),
  rotate: vi.fn(),
  createLinearGradient: vi.fn().mockReturnValue({ addColorStop: vi.fn() }),
  clip: vi.fn(),
  rect: vi.fn(),
  closePath: vi.fn(),
  getImageData: vi.fn().mockReturnValue({ data: [] }),
  putImageData: vi.fn(),
  drawImage: vi.fn(),
  isPointInPath: vi.fn(),
  isPointInStroke: vi.fn(),
}) as never;

describe('ErrorGroupDetail', () => {
  it('renders the error message title', () => {
    renderWithProviders(<ErrorGroupDetail />);
    expect(screen.getByText('NullReferenceException in UserService')).toBeInTheDocument();
  });

  it('renders severity and status chips', () => {
    renderWithProviders(<ErrorGroupDetail />);
    const chips = screen.getAllByText('Critical');
    expect(chips.length).toBeGreaterThanOrEqual(1);
    const activeChips = screen.getAllByText('Active');
    expect(activeChips.length).toBeGreaterThanOrEqual(1);
  });

  it('renders metadata', () => {
    renderWithProviders(<ErrorGroupDetail />);
    expect(screen.getByText('Production ES')).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('abc123def456')).toBeInTheDocument();
  });

  it('renders severity and status dropdowns', () => {
    renderWithProviders(<ErrorGroupDetail />);
    const severityLabels = screen.getAllByText('Severity');
    expect(severityLabels.length).toBeGreaterThanOrEqual(1);
    const statusLabels = screen.getAllByText('Status');
    expect(statusLabels.length).toBeGreaterThanOrEqual(1);
  });

  it('renders stack trace in accordion', () => {
    renderWithProviders(<ErrorGroupDetail />);
    expect(screen.getByText('Stack Trace')).toBeInTheDocument();
  });

  it('shows sampling info when sampleRatio < 1', () => {
    renderWithProviders(<ErrorGroupDetail />);
    expect(screen.getByText(/sampling/i)).toBeInTheDocument();
  });

  it('renders a canvas element for the chart', () => {
    const { container } = renderWithProviders(<ErrorGroupDetail />);
    expect(container.querySelector('canvas')).toBeInTheDocument();
  });
});
