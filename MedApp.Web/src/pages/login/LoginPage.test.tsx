import {screen, waitFor} from '@testing-library/react';
import {json, mockFetch, renderWithApp} from '../../test/render';
import {LoginPage} from './LoginPage';

describe('LoginPage', () => {
  it('signs in with the entered credentials and goes to the patients list', async () => {
    const fetch = mockFetch(() =>
      json({sessionId: '00000000-0000-0000-0000-000000000001', expiresAtUtc: '2030-01-01T00:00:00Z'}),
    );
    const {user, router} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.type(await screen.findByLabelText(/username/i), 'test-admin');
    await user.type(screen.getByLabelText(/password/i), 'Synthetic-Passw0rd');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
    const request = fetch.mock.calls[0][0] as Request;
    expect(new URL(request.url).pathname).toBe('/api/auth/login');
    expect(request.method).toBe('POST');
    expect(await request.json()).toEqual({username: 'test-admin', password: 'Synthetic-Passw0rd'});
  });

  it('shows validation errors on their fields', async () => {
    mockFetch(() =>
      json(
        {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: {Username: ['Username must not be empty.'], Password: ['Password must not be empty.']},
        },
        400,
      ),
    );
    const {user} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.click(await screen.findByRole('button', {name: /sign in/i}));

    expect(await screen.findByText('Username must not be empty.')).toBeInTheDocument();
    expect(screen.getByText('Password must not be empty.')).toBeInTheDocument();
    expect(screen.queryByText('Invalid credentials')).not.toBeInTheDocument();
  });

  it.each([
    ['a wrong username or password', 401],
    ['a server error', 500],
  ])('shows "Invalid credentials" for %s', async (_, status) => {
    mockFetch(() => json({title: 'Invalid credentials.', status}, status));
    const {user, router} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.type(await screen.findByLabelText(/username/i), 'test-admin');
    await user.type(screen.getByLabelText(/password/i), 'wrong');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    expect(await screen.findByText('Invalid credentials')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });
});
