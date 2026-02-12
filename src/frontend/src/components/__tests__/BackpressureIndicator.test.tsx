import { screen } from '@testing-library/react';
import { renderWithProviders } from '../../test/test-utils';

describe('BackpressureIndicator - low budget', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('shows warning when data source has low sampling budget', async () => {
    vi.doMock('../../api/hooks/useDataSources', () => ({
      useDataSources: () => ({
        data: [
          {
            id: 'ds-1',
            name: 'Production ES',
            adapterType: 'Elasticsearch',
            connectionString: 'http://localhost:9200',
            samplingBudget: 0.3,
            enabled: true,
            createdAt: '2025-01-01T00:00:00Z',
            updatedAt: null,
          },
        ],
        isLoading: false,
      }),
    }));

    const { default: Indicator } = await import('../BackpressureIndicator');
    renderWithProviders(<Indicator />);
    expect(screen.getByText('Backpressure Detected')).toBeInTheDocument();
    expect(screen.getByText(/Production ES \(30%\)/)).toBeInTheDocument();
  });
});

describe('BackpressureIndicator - normal budget', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('is hidden when all data sources have normal budget', async () => {
    vi.doMock('../../api/hooks/useDataSources', () => ({
      useDataSources: () => ({
        data: [
          {
            id: 'ds-1',
            name: 'Production ES',
            adapterType: 'Elasticsearch',
            connectionString: 'http://localhost:9200',
            samplingBudget: 0.9,
            enabled: true,
            createdAt: '2025-01-01T00:00:00Z',
            updatedAt: null,
          },
        ],
        isLoading: false,
      }),
    }));

    const { default: Indicator } = await import('../BackpressureIndicator');
    const { container } = renderWithProviders(<Indicator />);
    expect(container.querySelector('.MuiAlert-root')).not.toBeInTheDocument();
  });
});

describe('BackpressureIndicator - no data', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('renders nothing when no data sources loaded', async () => {
    vi.doMock('../../api/hooks/useDataSources', () => ({
      useDataSources: () => ({
        data: undefined,
        isLoading: true,
      }),
    }));

    const { default: Indicator } = await import('../BackpressureIndicator');
    const { container } = renderWithProviders(<Indicator />);
    expect(container.querySelector('.MuiAlert-root')).not.toBeInTheDocument();
  });
});
