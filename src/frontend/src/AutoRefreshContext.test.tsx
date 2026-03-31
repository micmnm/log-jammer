import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders, userEvent } from './test/test-utils';
import { AutoRefreshProvider, useAutoRefresh } from './AutoRefreshContext';

const STORAGE_KEY = 'logjammer-auto-refresh';

function TestComponent() {
  const { refreshInterval, setRefreshInterval } = useAutoRefresh();

  return (
    <div>
      <div data-testid="current-interval">{refreshInterval}</div>
      <button onClick={() => setRefreshInterval(0)}>Set Off</button>
      <button onClick={() => setRefreshInterval(60_000)}>Set 1min</button>
      <button onClick={() => setRefreshInterval(300_000)}>Set 5min</button>
    </div>
  );
}

describe('AutoRefreshContext', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  afterEach(() => {
    localStorage.clear();
  });

  describe('AutoRefreshProvider', () => {
    it('defaults to 0 (off) when localStorage is empty', () => {
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );
      expect(screen.getByTestId('current-interval')).toHaveTextContent('0');
    });

    it('reads valid interval from localStorage on mount', () => {
      localStorage.setItem(STORAGE_KEY, '60000');
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );
      expect(screen.getByTestId('current-interval')).toHaveTextContent('60000');
    });

    it('reads 5min interval from localStorage on mount', () => {
      localStorage.setItem(STORAGE_KEY, '300000');
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );
      expect(screen.getByTestId('current-interval')).toHaveTextContent('300000');
    });

    it('ignores invalid localStorage values, defaults to 0', () => {
      localStorage.setItem(STORAGE_KEY, 'invalid');
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );
      expect(screen.getByTestId('current-interval')).toHaveTextContent('0');
    });

    it('ignores invalid interval numbers, defaults to 0', () => {
      localStorage.setItem(STORAGE_KEY, '99999');
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );
      expect(screen.getByTestId('current-interval')).toHaveTextContent('0');
    });

    it('ignores null localStorage value, defaults to 0', () => {
      localStorage.setItem(STORAGE_KEY, 'null');
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );
      expect(screen.getByTestId('current-interval')).toHaveTextContent('0');
    });

    it('provides correct context value to children', () => {
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );
      expect(screen.getByTestId('current-interval')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Set Off' })).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: 'Set 1min' }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: 'Set 5min' }),
      ).toBeInTheDocument();
    });
  });

  describe('setRefreshInterval', () => {
    it('updates state when called', async () => {
      const user = userEvent.setup();
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );

      expect(screen.getByTestId('current-interval')).toHaveTextContent('0');
      await user.click(screen.getByRole('button', { name: 'Set 1min' }));
      expect(screen.getByTestId('current-interval')).toHaveTextContent('60000');
    });

    it('persists to localStorage when called', async () => {
      const user = userEvent.setup();
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );

      await user.click(screen.getByRole('button', { name: 'Set 5min' }));
      expect(localStorage.getItem(STORAGE_KEY)).toBe('300000');
    });

    it('persists off state to localStorage', async () => {
      const user = userEvent.setup();
      localStorage.setItem(STORAGE_KEY, '60000');
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );

      await user.click(screen.getByRole('button', { name: 'Set Off' }));
      expect(localStorage.getItem(STORAGE_KEY)).toBe('0');
    });

    it('handles multiple state changes in sequence', async () => {
      const user = userEvent.setup();
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );

      await user.click(screen.getByRole('button', { name: 'Set 1min' }));
      expect(screen.getByTestId('current-interval')).toHaveTextContent('60000');
      expect(localStorage.getItem(STORAGE_KEY)).toBe('60000');

      await user.click(screen.getByRole('button', { name: 'Set 5min' }));
      expect(screen.getByTestId('current-interval')).toHaveTextContent('300000');
      expect(localStorage.getItem(STORAGE_KEY)).toBe('300000');

      await user.click(screen.getByRole('button', { name: 'Set Off' }));
      expect(screen.getByTestId('current-interval')).toHaveTextContent('0');
      expect(localStorage.getItem(STORAGE_KEY)).toBe('0');
    });
  });

  describe('useAutoRefresh hook', () => {
    it('provides refreshInterval value', () => {
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );
      expect(screen.getByTestId('current-interval')).toHaveTextContent('0');
    });

    it('provides setRefreshInterval function', async () => {
      const user = userEvent.setup();
      renderWithProviders(
        <AutoRefreshProvider>
          <TestComponent />
        </AutoRefreshProvider>,
      );

      const button = screen.getByRole('button', { name: 'Set 1min' });
      expect(button).toBeInTheDocument();
      await user.click(button);
      expect(screen.getByTestId('current-interval')).toHaveTextContent('60000');
    });
  });
});
