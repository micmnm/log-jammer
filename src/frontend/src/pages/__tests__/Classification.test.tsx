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
      createdAt: '2025-01-02T00:00:00Z',
    },
    {
      id: 'q-3',
      knownErrorId: 'ke-3',
      message: 'Unknown parsing failure',
      stackTrace: null,
      suggestedTags: [],
      confidence: 0.2,
      createdAt: '2025-01-03T00:00:00Z',
    },
  ],
  totalCount: 3,
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
  });

  it('renders suggested tags', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('network')).toBeInTheDocument();
    expect(screen.getByText('payment')).toBeInTheDocument();
    expect(screen.getByText('database')).toBeInTheDocument();
  });

  it('calls approve mutation when Approve button clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);
    const approveButtons = screen.getAllByText('Approve');
    await user.click(approveButtons[0]);
    expect(mockApprove).toHaveBeenCalledWith({
      id: 'q-1',
      tagIds: ['t-1', 't-2'],
    });
  });

  it('opens reject dialog when Reject button clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);
    const rejectButtons = screen.getAllByText('Reject');
    await user.click(rejectButtons[0]);
    expect(screen.getByText('Reject Classification')).toBeInTheDocument();
    expect(screen.getByLabelText('Correct Tags')).toBeInTheDocument();
    expect(screen.getByLabelText('Reason (optional)')).toBeInTheDocument();
  });

  it('renders summary stats strip', () => {
    renderWithProviders(<Classification />);
    // Total pending
    expect(screen.getByText('Total Pending')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    // Confidence breakdown labels
    expect(screen.getByText('High ≥70%')).toBeInTheDocument();
    expect(screen.getByText('Medium 40–69%')).toBeInTheDocument();
    expect(screen.getByText('Low <40%')).toBeInTheDocument();
    // Each band shows count 1 (three stat boxes show "1")
    expect(screen.getAllByText('1')).toHaveLength(3);
    // Average confidence
    expect(screen.getByText('Avg Confidence')).toBeInTheDocument();
    expect(screen.getByText('53%')).toBeInTheDocument(); // avg of 85+55+20 = 160/3 ≈ 53
  });

  it('renders filter chips', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('ALL')).toBeInTheDocument();
    expect(screen.getByText('HIGH ≥70%')).toBeInTheDocument();
    expect(screen.getByText('MEDIUM 40–69%')).toBeInTheDocument();
    expect(screen.getByText('LOW <40%')).toBeInTheDocument();
  });

  it('filters items by confidence band when filter chip clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);

    // All 3 items visible initially
    expect(screen.getByText('Connection timeout in payment processor')).toBeInTheDocument();
    expect(screen.getByText('Null reference in user lookup')).toBeInTheDocument();
    expect(screen.getByText('Unknown parsing failure')).toBeInTheDocument();

    // Click HIGH filter
    await user.click(screen.getByText('HIGH ≥70%'));
    expect(screen.getByText('Connection timeout in payment processor')).toBeInTheDocument();
    expect(screen.queryByText('Null reference in user lookup')).not.toBeInTheDocument();
    expect(screen.queryByText('Unknown parsing failure')).not.toBeInTheDocument();

    // Click LOW filter
    await user.click(screen.getByText('LOW <40%'));
    expect(screen.queryByText('Connection timeout in payment processor')).not.toBeInTheDocument();
    expect(screen.getByText('Unknown parsing failure')).toBeInTheDocument();

    // Click ALL to reset
    await user.click(screen.getByText('ALL'));
    expect(screen.getByText('Connection timeout in payment processor')).toBeInTheDocument();
    expect(screen.getByText('Null reference in user lookup')).toBeInTheDocument();
    expect(screen.getByText('Unknown parsing failure')).toBeInTheDocument();
  });

  it('shows empty filter message when no items match', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);

    // Click MEDIUM filter - only q-2 (0.55) should match
    await user.click(screen.getByText('MEDIUM 40–69%'));
    expect(screen.getByText('Null reference in user lookup')).toBeInTheDocument();
    expect(screen.queryByText('Connection timeout in payment processor')).not.toBeInTheDocument();
  });
});
