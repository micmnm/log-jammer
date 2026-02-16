import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NotificationProvider, useNotification } from '../NotificationContext';

function TestTrigger() {
  const { showNotification } = useNotification();
  return (
    <button onClick={() => showNotification('Something went wrong', 'error')}>
      Trigger
    </button>
  );
}

describe('NotificationProvider', () => {
  it('shows a snackbar when showNotification is called', async () => {
    const user = userEvent.setup();
    render(
      <NotificationProvider>
        <TestTrigger />
      </NotificationProvider>,
    );

    await user.click(screen.getByText('Trigger'));

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('renders with success severity', async () => {
    const user = userEvent.setup();
    function SuccessTrigger() {
      const { showNotification } = useNotification();
      return (
        <button onClick={() => showNotification('Saved!', 'success')}>
          Save
        </button>
      );
    }

    render(
      <NotificationProvider>
        <SuccessTrigger />
      </NotificationProvider>,
    );

    await user.click(screen.getByText('Save'));

    expect(screen.getByText('Saved!')).toBeInTheDocument();
  });
});
