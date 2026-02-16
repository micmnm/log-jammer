import { screen } from '@testing-library/react';
import { renderWithProviders, userEvent } from '../../test/test-utils';
import Dashboard from '../Dashboard';

const mockNavigate = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock('../../api/hooks/useDashboard', () => ({
  useDashboardStats: () => ({
    firingCount: 5,
    errorGroupCount: 12,
    unclassifiedCount: 8,
    isLoading: false,
  }),
}));

vi.mock('../../components/AlertsFeed', () => ({
  default: () => <div data-testid="alerts-feed">AlertsFeed</div>,
}));

vi.mock('../../components/BackpressureIndicator', () => ({
  default: () => <div data-testid="backpressure">BackpressureIndicator</div>,
}));

describe('Dashboard', () => {
  beforeEach(() => {
    mockNavigate.mockClear();
  });

  it('renders stat cards with values', () => {
    renderWithProviders(<Dashboard />);
    expect(screen.getByText('Firing Alerts')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('Error Groups')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Unclassified')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
  });

  it('navigates to /alerts when Firing Alerts card is clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Dashboard />);
    await user.click(screen.getByText('Firing Alerts'));
    expect(mockNavigate).toHaveBeenCalledWith('/alerts');
  });

  it('navigates to /error-groups when Error Groups card is clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Dashboard />);
    await user.click(screen.getByText('Error Groups'));
    expect(mockNavigate).toHaveBeenCalledWith('/error-groups');
  });

  it('navigates to /classification when Unclassified card is clicked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Dashboard />);
    await user.click(screen.getByText('Unclassified'));
    expect(mockNavigate).toHaveBeenCalledWith('/classification');
  });
});
