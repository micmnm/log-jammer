import { screen, waitFor } from '@testing-library/react';
import { renderWithProviders, userEvent } from '../../test/test-utils';
import { useAuth } from '../AuthContext';

const TOKEN_KEY = 'logjammer_token';

function TestComponent() {
  const { isAuthenticated, login, logout } = useAuth();
  return (
    <div>
      <span data-testid="auth-status">{isAuthenticated ? 'authenticated' : 'anonymous'}</span>
      <button onClick={() => login('admin', 'pass').catch(() => {})}>Login</button>
      <button onClick={logout}>Logout</button>
    </div>
  );
}

describe('AuthContext', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('starts as anonymous when no token in localStorage', () => {
    renderWithProviders(<TestComponent />);
    expect(screen.getByTestId('auth-status')).toHaveTextContent('anonymous');
  });

  it('starts as authenticated when token exists in localStorage', () => {
    localStorage.setItem(TOKEN_KEY, 'existing-token');
    renderWithProviders(<TestComponent />);
    expect(screen.getByTestId('auth-status')).toHaveTextContent('authenticated');
  });

  it('login stores token and sets authenticated on success', async () => {
    const user = userEvent.setup();
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(JSON.stringify({ token: 'test-token' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    renderWithProviders(<TestComponent />);
    await user.click(screen.getByText('Login'));

    await waitFor(() => {
      expect(screen.getByTestId('auth-status')).toHaveTextContent('authenticated');
    });
    expect(localStorage.getItem(TOKEN_KEY)).toBe('test-token');
  });

  it('login throws on invalid credentials', async () => {
    const user = userEvent.setup();
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(null, { status: 401 }),
    );

    renderWithProviders(<TestComponent />);

    // login will throw, but the component doesn't catch it — just verify state stays anonymous
    await user.click(screen.getByText('Login'));

    await waitFor(() => {
      expect(screen.getByTestId('auth-status')).toHaveTextContent('anonymous');
    });
  });

  it('logout clears token and sets anonymous', async () => {
    const user = userEvent.setup();
    localStorage.setItem(TOKEN_KEY, 'existing-token');

    renderWithProviders(<TestComponent />);
    expect(screen.getByTestId('auth-status')).toHaveTextContent('authenticated');

    await user.click(screen.getByText('Logout'));

    await waitFor(() => {
      expect(screen.getByTestId('auth-status')).toHaveTextContent('anonymous');
    });
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
  });
});
