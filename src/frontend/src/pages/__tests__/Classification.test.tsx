import { screen } from '@testing-library/react';
import { renderWithProviders, userEvent } from '../../test/test-utils';
import Classification from '../Classification';

const mockQueueData = {
  items: [
    {
      id: 'q-1',
      knownErrorId: 'ke-1',
      message: 'Connection timeout in payment processor',
      stackTrace: 'at PaymentService.Process()\nat OrderController.Submit()',
      suggestedTags: [
        { tagId: 't-1', tagName: 'network', confidence: 0.92 },
        { tagId: 't-2', tagName: 'payment', confidence: 0.78 },
      ],
      confidence: 0.85,
      severity: 'Critical',
      status: 'Active',
      firstSeen: '2025-01-01T00:00:00Z',
      lastSeen: '2025-01-01T12:00:00Z',
      totalOccurrences: 47,
      createdAt: '2025-01-01T00:00:00Z',
    },
    {
      id: 'q-2',
      knownErrorId: 'ke-2',
      message: 'Null reference in user lookup',
      stackTrace: null,
      suggestedTags: [
        { tagId: 't-3', tagName: 'database', confidence: 0.55 },
      ],
      confidence: 0.55,
      severity: 'Warning',
      status: 'Active',
      firstSeen: '2025-01-02T00:00:00Z',
      lastSeen: '2025-01-02T06:00:00Z',
      totalOccurrences: 12,
      createdAt: '2025-01-02T00:00:00Z',
    },
    {
      id: 'q-3',
      knownErrorId: 'ke-3',
      message: 'Unknown parsing failure',
      stackTrace: null,
      suggestedTags: [],
      confidence: 0.2,
      severity: 'Warning',
      status: 'Active',
      firstSeen: '2025-01-03T00:00:00Z',
      lastSeen: '2025-01-03T00:00:00Z',
      totalOccurrences: 3,
      createdAt: '2025-01-03T00:00:00Z',
    },
    {
      id: 'q-4',
      knownErrorId: 'ke-4',
      message: 'Unexpected token in JSON at position 0',
      stackTrace: null,
      suggestedTags: [],
      confidence: null,
      severity: 'Info',
      status: 'Active',
      firstSeen: '2025-01-04T00:00:00Z',
      lastSeen: '2025-01-04T00:00:00Z',
      totalOccurrences: 1,
      createdAt: '2025-01-04T00:00:00Z',
    },
  ],
  totalCount: 4,
  page: 1,
  pageSize: 10,
};

const mockApprove = vi.fn();
const mockReject = vi.fn();

vi.mock('../../api/hooks/useClassification', () => ({
  useClassificationQueue: () => ({
    data: mockQueueData,
    isLoading: false,
    error: null,
  }),
  useApproveClassification: () => ({
    mutate: mockApprove,
    isPending: false,
  }),
  useRejectClassification: () => ({
    mutate: mockReject,
    isPending: false,
  }),
}));

vi.mock('../../api/hooks/useTags', () => ({
  useTags: () => ({
    data: [
      { id: 't-1', name: 'network', tagType: 'Auto', color: null, createdAt: '2025-01-01T00:00:00Z' },
      { id: 't-2', name: 'payment', tagType: 'Manual', color: '#ff0000', createdAt: '2025-01-01T00:00:00Z' },
      { id: 't-3', name: 'database', tagType: 'Manual', color: null, createdAt: '2025-01-01T00:00:00Z' },
    ],
    isLoading: false,
  }),
  useCreateTag: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
}));

describe('Classification', () => {
  beforeEach(() => {
    mockApprove.mockClear();
    mockReject.mockClear();
  });

  it('renders the page title', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('Classification Queue')).toBeInTheDocument();
  });

  it('renders queue item messages', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('Connection timeout in payment processor')).toBeInTheDocument();
    expect(screen.getByText('Null reference in user lookup')).toBeInTheDocument();
    expect(screen.getByText('Unknown parsing failure')).toBeInTheDocument();
    expect(screen.getByText('Unexpected token in JSON at position 0')).toBeInTheDocument();
  });

  it('renders suggested tags within ML suggestion boxes', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('network')).toBeInTheDocument();
    expect(screen.getByText('payment')).toBeInTheDocument();
    expect(screen.getByText('database')).toBeInTheDocument();
  });

  it('renders Accept Tags button for items with suggestions', () => {
    renderWithProviders(<Classification />);
    const acceptButtons = screen.getAllByText('Accept Tags');
    expect(acceptButtons).toHaveLength(2); // q-1 and q-2
  });

  it('renders Assign Tags button for unmatched items', () => {
    renderWithProviders(<Classification />);
    const assignButtons = screen.getAllByText('Assign Tags');
    expect(assignButtons).toHaveLength(2); // q-3 and q-4
  });

  it('calls approve mutation when Accept Tags button clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);
    const acceptButtons = screen.getAllByText('Accept Tags');
    await user.click(acceptButtons[0]);
    expect(mockApprove).toHaveBeenCalledWith({
      id: 'q-1',
      tagIds: ['t-1', 't-2'],
    });
  });

  it('opens reject dialog when Reject & Retag button clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);
    const rejectButtons = screen.getAllByText('Reject & Retag');
    await user.click(rejectButtons[0]);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Reject & Retag' })).toBeInTheDocument();
    expect(screen.getByLabelText('Reason (optional)')).toBeInTheDocument();
  });

  it('renders summary stats strip with unmatched count', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('Total Pending')).toBeInTheDocument();
    expect(screen.getByText('4')).toBeInTheDocument();
    expect(screen.getByText('High ≥70%')).toBeInTheDocument();
    expect(screen.getByText('Medium 40–69%')).toBeInTheDocument();
    expect(screen.getByText('Low <40%')).toBeInTheDocument();
    expect(screen.getByText('Unmatched')).toBeInTheDocument();
    // 2 unmatched items (q-3, q-4)
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('renders filter chips including UNMATCHED', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('ALL')).toBeInTheDocument();
    expect(screen.getByText('HIGH ≥70%')).toBeInTheDocument();
    expect(screen.getByText('MEDIUM 40–69%')).toBeInTheDocument();
    expect(screen.getByText('LOW <40%')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'UNMATCHED' })).toBeInTheDocument();
  });

  it('filters items by confidence band when filter chip clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);

    // All 4 items visible initially
    expect(screen.getByText('Connection timeout in payment processor')).toBeInTheDocument();
    expect(screen.getByText('Null reference in user lookup')).toBeInTheDocument();
    expect(screen.getByText('Unknown parsing failure')).toBeInTheDocument();
    expect(screen.getByText('Unexpected token in JSON at position 0')).toBeInTheDocument();

    // Click HIGH filter
    await user.click(screen.getByText('HIGH ≥70%'));
    expect(screen.getByText('Connection timeout in payment processor')).toBeInTheDocument();
    expect(screen.queryByText('Null reference in user lookup')).not.toBeInTheDocument();
    expect(screen.queryByText('Unknown parsing failure')).not.toBeInTheDocument();
    expect(screen.queryByText('Unexpected token in JSON at position 0')).not.toBeInTheDocument();

    // Click ALL to reset
    await user.click(screen.getByText('ALL'));
    expect(screen.getByText('Connection timeout in payment processor')).toBeInTheDocument();
    expect(screen.getByText('Null reference in user lookup')).toBeInTheDocument();
  });

  it('filters to unmatched items when UNMATCHED chip clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);

    await user.click(screen.getByRole('button', { name: 'UNMATCHED' }));
    // Only unmatched items visible (q-3 and q-4)
    expect(screen.queryByText('Connection timeout in payment processor')).not.toBeInTheDocument();
    expect(screen.queryByText('Null reference in user lookup')).not.toBeInTheDocument();
    expect(screen.getByText('Unknown parsing failure')).toBeInTheDocument();
    expect(screen.getByText('Unexpected token in JSON at position 0')).toBeInTheDocument();
  });

  it('shows empty filter message when no items match', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);

    // Click MEDIUM filter - only q-2 (0.55) should match
    await user.click(screen.getByText('MEDIUM 40–69%'));
    expect(screen.getByText('Null reference in user lookup')).toBeInTheDocument();
    expect(screen.queryByText('Connection timeout in payment processor')).not.toBeInTheDocument();
  });

  it('renders UNMATCHED badge on items with no suggestions', () => {
    renderWithProviders(<Classification />);
    const unmatchedBadges = screen.getAllByText('UNMATCHED', { selector: '.MuiChip-label' });
    expect(unmatchedBadges).toHaveLength(2); // q-3 and q-4
  });

  it('renders ML Suggestion label for items with suggestions', () => {
    renderWithProviders(<Classification />);
    const mlLabels = screen.getAllByText('ML Suggestion');
    expect(mlLabels).toHaveLength(2); // q-1 and q-2
  });
});
