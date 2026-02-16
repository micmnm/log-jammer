import { screen, within } from '@testing-library/react';
import { renderWithProviders, userEvent } from '../../test/test-utils';
import ClassificationQueueCard from '../ClassificationQueueCard';
import type { ClassificationQueueResponse } from '../../api/types';

const mockApprove = vi.fn();
const mockReject = vi.fn();
const mockCreateTag = vi.fn();

vi.mock('../../api/hooks/useClassification', () => ({
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
    ],
    isLoading: false,
  }),
  useCreateTag: () => ({
    mutate: mockCreateTag,
    isPending: false,
  }),
}));

const itemWithSuggestions: ClassificationQueueResponse = {
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
};

const itemUnmatched: ClassificationQueueResponse = {
  id: 'q-2',
  knownErrorId: 'ke-2',
  message: 'Unexpected token in JSON at position 0',
  stackTrace: null,
  suggestedTags: [],
  confidence: null,
  severity: 'Warning',
  status: 'Active',
  firstSeen: '2025-01-03T00:00:00Z',
  lastSeen: '2025-01-03T00:00:00Z',
  totalOccurrences: 3,
  createdAt: '2025-01-03T00:00:00Z',
};

describe('ClassificationQueueCard', () => {
  beforeEach(() => {
    mockApprove.mockClear();
    mockReject.mockClear();
    mockCreateTag.mockClear();
  });

  describe('ML suggestion state', () => {
    it('renders ML Suggestion box with correct labels', () => {
      renderWithProviders(<ClassificationQueueCard item={itemWithSuggestions} />);
      expect(screen.getByText('ML Suggestion')).toBeInTheDocument();
      expect(screen.getByText('Classifier suggests:')).toBeInTheDocument();
    });

    it('renders per-tag confidence bars', () => {
      renderWithProviders(<ClassificationQueueCard item={itemWithSuggestions} />);
      expect(screen.getByText('network')).toBeInTheDocument();
      expect(screen.getByText('92%')).toBeInTheDocument();
      expect(screen.getByText('payment')).toBeInTheDocument();
      expect(screen.getByText('78%')).toBeInTheDocument();
    });

    it('renders overall confidence', () => {
      renderWithProviders(<ClassificationQueueCard item={itemWithSuggestions} />);
      expect(screen.getByText('Overall confidence: 85%')).toBeInTheDocument();
    });

    it('renders Accept Tags and Reject & Retag buttons', () => {
      renderWithProviders(<ClassificationQueueCard item={itemWithSuggestions} />);
      expect(screen.getByText('Accept Tags')).toBeInTheDocument();
      expect(screen.getByText('Reject & Retag')).toBeInTheDocument();
    });

    it('does not render UNMATCHED badge', () => {
      renderWithProviders(<ClassificationQueueCard item={itemWithSuggestions} />);
      expect(screen.queryByText('UNMATCHED')).not.toBeInTheDocument();
    });

    it('renders error context line with severity and occurrences', () => {
      renderWithProviders(<ClassificationQueueCard item={itemWithSuggestions} />);
      expect(screen.getByText('Critical')).toBeInTheDocument();
      expect(screen.getByText('47 occurrences')).toBeInTheDocument();
    });

    it('renders stack trace accordion', () => {
      renderWithProviders(<ClassificationQueueCard item={itemWithSuggestions} />);
      expect(screen.getByText('Stack Trace')).toBeInTheDocument();
    });
  });

  describe('UNMATCHED state', () => {
    it('renders UNMATCHED badge when suggestedTags is empty', () => {
      renderWithProviders(<ClassificationQueueCard item={itemUnmatched} />);
      expect(screen.getByText('UNMATCHED')).toBeInTheDocument();
    });

    it('renders "No similar errors" message', () => {
      renderWithProviders(<ClassificationQueueCard item={itemUnmatched} />);
      expect(screen.getByText('No similar errors found in the classifier. Assign tags manually.')).toBeInTheDocument();
    });

    it('renders Assign Tags button instead of Accept/Reject', () => {
      renderWithProviders(<ClassificationQueueCard item={itemUnmatched} />);
      expect(screen.getByText('Assign Tags')).toBeInTheDocument();
      expect(screen.queryByText('Accept Tags')).not.toBeInTheDocument();
      expect(screen.queryByText('Reject & Retag')).not.toBeInTheDocument();
    });

    it('does not render ML Suggestion box', () => {
      renderWithProviders(<ClassificationQueueCard item={itemUnmatched} />);
      expect(screen.queryByText('ML Suggestion')).not.toBeInTheDocument();
      expect(screen.queryByText('Classifier suggests:')).not.toBeInTheDocument();
    });

    it('renders error context line', () => {
      renderWithProviders(<ClassificationQueueCard item={itemUnmatched} />);
      expect(screen.getByText('Warning')).toBeInTheDocument();
      expect(screen.getByText('3 occurrences')).toBeInTheDocument();
    });
  });

  describe('Accept Tags action', () => {
    it('calls approve mutation with suggested tag IDs', async () => {
      const user = userEvent.setup();
      renderWithProviders(<ClassificationQueueCard item={itemWithSuggestions} />);
      await user.click(screen.getByText('Accept Tags'));
      expect(mockApprove).toHaveBeenCalledWith({
        id: 'q-1',
        tagIds: ['t-1', 't-2'],
      });
    });
  });

  describe('Reject & Retag dialog', () => {
    it('opens with Reject & Retag title and shows reason field', async () => {
      const user = userEvent.setup();
      renderWithProviders(<ClassificationQueueCard item={itemWithSuggestions} />);
      await user.click(screen.getByText('Reject & Retag'));
      const dialog = screen.getByRole('dialog');
      expect(within(dialog).getByText('Reject & Retag')).toBeInTheDocument();
      expect(within(dialog).getByLabelText('Reason (optional)')).toBeInTheDocument();
      expect(within(dialog).getByText('Confirm Reject')).toBeInTheDocument();
    });
  });

  describe('Assign Tags dialog', () => {
    it('opens with Assign Tags title and no reason field', async () => {
      const user = userEvent.setup();
      renderWithProviders(<ClassificationQueueCard item={itemUnmatched} />);
      await user.click(screen.getByText('Assign Tags'));
      const dialog = screen.getByRole('dialog');
      expect(within(dialog).getByText('Assign Tags')).toBeInTheDocument();
      expect(within(dialog).queryByLabelText('Reason (optional)')).not.toBeInTheDocument();
      expect(within(dialog).getByText('Confirm Assign')).toBeInTheDocument();
    });
  });

  describe('Inline tag creation', () => {
    it('shows Create option when typing a new tag name', async () => {
      const user = userEvent.setup();
      renderWithProviders(<ClassificationQueueCard item={itemUnmatched} />);

      // Open the assign dialog
      await user.click(screen.getByText('Assign Tags'));
      const dialog = screen.getByRole('dialog');

      // Type a new tag name in the autocomplete
      const input = within(dialog).getByLabelText('Tags');
      await user.type(input, 'foobar');

      // Should show a "Create" option
      expect(screen.getByText('Create "foobar"')).toBeInTheDocument();
    });

    it('shows color picker when Create option is selected', async () => {
      const user = userEvent.setup();
      renderWithProviders(<ClassificationQueueCard item={itemUnmatched} />);

      await user.click(screen.getByText('Assign Tags'));
      const dialog = screen.getByRole('dialog');
      const input = within(dialog).getByLabelText('Tags');
      await user.type(input, 'foobar');
      await user.click(screen.getByText('Create "foobar"'));

      // Color picker section should appear
      expect(screen.getByText(/Create tag "foobar"/)).toBeInTheDocument();
      // Create and Cancel buttons in the color picker
      const createButtons = within(dialog).getAllByText('Create');
      expect(createButtons.length).toBeGreaterThanOrEqual(1);
    });

    it('calls createTag mutation when Create button clicked', async () => {
      const user = userEvent.setup();
      renderWithProviders(<ClassificationQueueCard item={itemUnmatched} />);

      await user.click(screen.getByText('Assign Tags'));
      const dialog = screen.getByRole('dialog');
      const input = within(dialog).getByLabelText('Tags');
      await user.type(input, 'foobar');
      await user.click(screen.getByText('Create "foobar"'));

      // Click the Create button in the color picker section
      const createButtons = within(dialog).getAllByText('Create');
      // The last "Create" button is in the color picker section
      await user.click(createButtons[createButtons.length - 1]);

      expect(mockCreateTag).toHaveBeenCalledWith(
        { name: 'foobar', color: '#2196f3' },
        expect.objectContaining({ onSuccess: expect.any(Function) }),
      );
    });
  });
});
