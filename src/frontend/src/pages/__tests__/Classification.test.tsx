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
  ],
  totalCount: 1,
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

  it('renders queue item message', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('Connection timeout in payment processor')).toBeInTheDocument();
  });

  it('renders suggested tags', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('network')).toBeInTheDocument();
    expect(screen.getByText('payment')).toBeInTheDocument();
  });

  it('calls approve mutation when Approve button clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);
    await user.click(screen.getByText('Approve'));
    expect(mockApprove).toHaveBeenCalledWith({
      id: 'q-1',
      tagIds: ['t-1', 't-2'],
    });
  });

  it('opens reject dialog when Reject button clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Classification />);
    await user.click(screen.getByText('Reject'));
    expect(screen.getByText('Reject Classification')).toBeInTheDocument();
    expect(screen.getByLabelText('Correct Tags')).toBeInTheDocument();
    expect(screen.getByLabelText('Reason (optional)')).toBeInTheDocument();
  });

  it('shows confidence scores', () => {
    renderWithProviders(<Classification />);
    expect(screen.getByText('92%')).toBeInTheDocument();
    expect(screen.getByText('78%')).toBeInTheDocument();
    expect(screen.getByText('85%')).toBeInTheDocument();
  });
});
