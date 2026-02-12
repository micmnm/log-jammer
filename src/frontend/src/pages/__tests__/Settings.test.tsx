import { screen } from '@testing-library/react';
import { renderWithProviders, userEvent } from '../../test/test-utils';
import Settings from '../Settings';

const mockRules = [
  {
    id: 'r-1',
    knownErrorId: null,
    knownErrorMessage: null,
    thresholdType: 'Absolute' as const,
    thresholdValue: 10,
    windowMinutes: 5,
    lookbackMinutes: 1440,
    enabled: true,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
  },
];

const mockTags = [
  { id: 't-1', name: 'network', tagType: 'Auto' as const, color: '#ff0000', createdAt: '2025-01-01T00:00:00Z' },
  { id: 't-2', name: 'database', tagType: 'Manual' as const, color: null, createdAt: '2025-01-01T00:00:00Z' },
];

const mockConfigs = [
  { key: 'SimilarityThreshold', value: '0.85', description: 'Minimum similarity', updatedAt: '2025-01-01T00:00:00Z' },
  { key: 'AutoTagConfidenceThreshold', value: '0.7', description: 'Auto tag threshold', updatedAt: '2025-01-01T00:00:00Z' },
];

vi.mock('../../api/hooks/useSpikeDetectionRules', () => ({
  useSpikeDetectionRules: () => ({
    data: mockRules,
    isLoading: false,
  }),
  useCreateSpikeDetectionRule: () => ({
    mutate: vi.fn(),
  }),
  useUpdateSpikeDetectionRule: () => ({
    mutate: vi.fn(),
  }),
  useDeleteSpikeDetectionRule: () => ({
    mutate: vi.fn(),
  }),
}));

vi.mock('../../api/hooks/useTags', () => ({
  useTags: () => ({
    data: mockTags,
    isLoading: false,
  }),
  useCreateTag: () => ({
    mutate: vi.fn(),
  }),
  useUpdateTag: () => ({
    mutate: vi.fn(),
  }),
  useDeleteTag: () => ({
    mutate: vi.fn(),
  }),
}));

vi.mock('../../api/hooks/useConfiguration', () => ({
  useConfiguration: () => ({
    data: mockConfigs,
    isLoading: false,
  }),
  useUpdateConfiguration: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
}));

describe('Settings', () => {
  it('renders the page title', () => {
    renderWithProviders(<Settings />);
    expect(screen.getByText('Settings')).toBeInTheDocument();
  });

  it('renders all tabs', () => {
    renderWithProviders(<Settings />);
    expect(screen.getByRole('tab', { name: 'Rules' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Tags' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Classification' })).toBeInTheDocument();
  });

  it('shows Rules tab by default with rule data', () => {
    renderWithProviders(<Settings />);
    expect(screen.getByText('Global Default')).toBeInTheDocument();
    expect(screen.getByText('Absolute')).toBeInTheDocument();
  });

  it('switches to Tags tab', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Settings />);
    await user.click(screen.getByRole('tab', { name: 'Tags' }));
    expect(screen.getByText('network')).toBeInTheDocument();
    expect(screen.getByText('database')).toBeInTheDocument();
  });

  it('switches to Classification tab', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Settings />);
    await user.click(screen.getByRole('tab', { name: 'Classification' }));
    expect(screen.getByText('SimilarityThreshold')).toBeInTheDocument();
    expect(screen.getByText('AutoTagConfidenceThreshold')).toBeInTheDocument();
  });

  it('shows Add Rule button on Rules tab', () => {
    renderWithProviders(<Settings />);
    expect(screen.getByText('Add Rule')).toBeInTheDocument();
  });

  it('shows Add Tag button on Tags tab', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Settings />);
    await user.click(screen.getByRole('tab', { name: 'Tags' }));
    expect(screen.getByText('Add Tag')).toBeInTheDocument();
  });

  it('shows Save All button on Classification tab', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Settings />);
    await user.click(screen.getByRole('tab', { name: 'Classification' }));
    expect(screen.getByText('Save All')).toBeInTheDocument();
  });
});
