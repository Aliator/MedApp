import {screen} from '@testing-library/react';
import {renderWithApp, isDisabled, openDialog} from '../../test/render';
import {LoginPage} from './LoginPage';

describe('Forgot password', () => {
  it('opens from the login page and asks for a valid email', async () => {
    const {user} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.click(await screen.findByRole('button', {name: 'Forgot password?'}));
    expect(await screen.findByRole('heading', {name: 'Reset your password'})).toBeInTheDocument();

    const send = screen.getByRole('button', {name: 'Send reset link'});
    expect(isDisabled(send)).toBe(true);

    await user.type(screen.getByLabelText(/email address/i), 'not-an-email');
    await user.click(send);

    // Shown on the field and repeated in a live region for screen readers.
    expect((await screen.findAllByText('Please enter a valid email address.')).length).toBeGreaterThan(0);
  });

  it('says a link was sent (a stand-in: the API has no reset endpoint yet)', async () => {
    const {user} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.click(await screen.findByRole('button', {name: 'Forgot password?'}));
    await user.type(await screen.findByLabelText(/email address/i), 'test-user@example.com');
    await user.click(screen.getByRole('button', {name: 'Send reset link'}));

    expect(await screen.findByText('Check your email', {}, {timeout: 3000})).toBeInTheDocument();
    expect(screen.getByText(/test-user@example.com/)).toBeInTheDocument();

    // The footer's Close, not the header's close (×) button, which has the same name.
    const close = screen.getAllByRole('button', {name: 'Close'});
    await user.click(close[close.length - 1]);
    expect(openDialog()).toBeNull();
  });
});
