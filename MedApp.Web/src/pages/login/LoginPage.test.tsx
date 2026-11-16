import {screen, waitFor} from '@testing-library/react';
import {calls, json, mockApi, mockFetch, renderApp, renderWithApp} from '../../test/render';
import {LoginPage, parseLoginSearch} from './LoginPage';

describe('LoginPage', () => {
  it('signs in with the entered credentials and goes to the welcome page', async () => {
    const fetch = mockFetch(() =>
      json({sessionId: '00000000-0000-0000-0000-000000000001', expiresAtUtc: '2030-01-01T00:00:00Z'}),
    );
    const {user, router} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.type(await screen.findByLabelText(/username/i), 'test-admin');
    await user.type(screen.getByLabelText(/^password/i), 'Synthetic-Passw0rd');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
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

  it('goes to the start page chosen in Account Settings', async () => {
    localStorage.setItem('medapp.startPage', 'procedures');
    mockFetch(() => json({sessionId: '00000000-0000-0000-0000-000000000001', expiresAtUtc: '2030-01-01T00:00:00Z'}));
    const {user, router} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.type(await screen.findByLabelText(/username/i), 'test-admin');
    await user.type(screen.getByLabelText(/^password/i), 'Synthetic-Passw0rd');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/procedures'));
  });

  it("brings back the account's settings first (signing out cleared them here), so it opens the account's start page", async () => {
    mockApi({
      'POST /api/auth/login': () => json({sessionId: '00000000-0000-0000-0000-000000000001', expiresAtUtc: '2030-01-01T00:00:00Z'}),
      'GET /api/auth/whoami': () => json({isAuthenticated: true, name: 'test-admin', roles: ['Admin', 'User']}),
      'GET /api/auth/me/settings': () => json({settings: JSON.stringify({'medapp.startPage': 'patients'})}),
    });
    const {user, router} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.type(await screen.findByLabelText(/username/i), 'test-admin');
    await user.type(screen.getByLabelText(/^password/i), 'Synthetic-Passw0rd');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
    expect(localStorage.getItem('medapp.startPage')).toBe('patients');
  });

  it("doesn't ask for the settings of someone who must change their password first", async () => {
    const fetch = mockApi({
      'POST /api/auth/login': () => json({sessionId: '00000000-0000-0000-0000-000000000001', expiresAtUtc: '2030-01-01T00:00:00Z'}),
      'GET /api/auth/whoami': () => json({isAuthenticated: true, name: 'test-new-user', roles: ['User'], mustChangePassword: true}),
    });
    const {user} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.type(await screen.findByLabelText(/username/i), 'test-new-user');
    await user.type(screen.getByLabelText(/^password/i), 'Synthetic-Passw0rd');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    await waitFor(() => expect(calls(fetch)).toContain('GET /api/auth/whoami'));
    expect(calls(fetch)).not.toContain('GET /api/auth/me/settings');
  });

  it.each([
    ['a wrong username or password', 401],
    ['a server error', 500],
  ])('shows "Invalid credentials" for %s', async (_, status) => {
    mockFetch(() => json({title: 'Invalid credentials.', status}, status));
    const {user, router} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.type(await screen.findByLabelText(/username/i), 'test-admin');
    await user.type(screen.getByLabelText(/^password/i), 'wrong');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    expect(await screen.findByText('Invalid credentials')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });

  it('says when an account is locked after too many wrong passwords, and for how long', async () => {
    const message = 'This account is locked after too many wrong passwords. Try again in 5 minutes.';
    mockFetch(() => json({title: message, status: 423}, 423));
    const {user, router} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.type(await screen.findByLabelText(/username/i), 'test-admin');
    await user.type(screen.getByLabelText(/^password/i), 'wrong');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(screen.queryByText('Invalid credentials')).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });

  it("says an account is locked when the API doesn't say for how long", async () => {
    mockFetch(() => new Response(null, {status: 423}));
    const {user} = renderWithApp(<LoginPage />, {path: '/login'});

    await user.type(await screen.findByLabelText(/username/i), 'test-admin');
    await user.type(screen.getByLabelText(/^password/i), 'wrong');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    expect(await screen.findByText('This account is locked. Try again later.')).toBeInTheDocument();
  });
});

describe('after a session timed out', () => {
  const TIMED_OUT = 'Your session timed out, so you were signed out. Sign in again to carry on.';

  it('says so, until a sign-in is tried', async () => {
    mockFetch(() => new Response(null, {status: 401}));
    const {user} = renderWithApp(<LoginPage isExpired />, {path: '/login'});

    expect(await screen.findByText(TIMED_OUT)).toBeInTheDocument();
    // An alert, so it's read out as it appears.
    expect(screen.getByText(TIMED_OUT).closest('[role="alert"]')).not.toBeNull();
    await user.type(screen.getByLabelText(/username/i), 'test-admin');
    await user.type(screen.getByLabelText(/^password/i), 'wrong');
    await user.click(screen.getByRole('button', {name: /sign in/i}));

    expect(await screen.findByText('Invalid credentials')).toBeInTheDocument();
    expect(screen.queryByText(TIMED_OUT)).not.toBeInTheDocument();
  });

  it('reads it from the URL, and nothing else', async () => {
    expect(parseLoginSearch({expired: true})).toEqual({expired: true});
    expect(parseLoginSearch({expired: 'true'})).toEqual({expired: true});
    expect(parseLoginSearch({expired: 'yes', other: 1})).toEqual({});

    mockFetch(() => json({isAuthenticated: false, roles: []}));
    renderApp({path: '/login?expired=true'});
    expect(await screen.findByText(TIMED_OUT)).toBeInTheDocument();
  });

  it('says nothing of it on a plain visit', async () => {
    renderWithApp(<LoginPage />, {path: '/login'});

    await screen.findByRole('heading', {name: 'Sign in'});
    expect(screen.queryByText(TIMED_OUT)).not.toBeInTheDocument();
  });
});
