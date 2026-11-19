import {screen, waitFor} from '@testing-library/react';
import {calls, expectAnnounced, findAlert, json, mockApi, renderApp, renderWithApp, withAnnouncements} from '../../test/render';
import {ForgotPasswordPage, LINK_REQUESTED, NO_EMAIL} from './ForgotPasswordPage';

function renderPage(forgot: Parameters<typeof mockApi>[0][string] = () => new Response(null, {status: 202}), canSendEmail = true) {
  const fetch = mockApi({
    'GET /api/auth/features': () => json({canSendEmail}),
    'POST /api/auth/password/forgot': forgot,
  });
  return {...renderWithApp(<ForgotPasswordPage />, {path: '/forgot-password'}), fetch};
}

type User = ReturnType<typeof renderPage>['user'];

async function ask(user: User, value = 'test-user') {
  await user.type(await screen.findByLabelText(/username or email address/i), value);
  await user.click(screen.getByRole('button', {name: 'Send Reset Link'}));
}

describe('ForgotPasswordPage', () => {
  it('asks for a username or email address, named for screen readers and the tab', async () => {
    renderPage();

    const field = await screen.findByLabelText(/username or email address/i);
    expect(screen.getByRole('heading', {name: 'Reset your password', level: 1})).toBeInTheDocument();
    expect(field).toHaveAttribute('autocomplete', 'username');
    expect(field).toHaveFocus();
    expect(document.title).toBe('Reset your password - MedApp');
    expect(screen.getByRole('link', {name: 'Back to sign in'})).toHaveAttribute('href', '/login');
  });

  it('asks the API for a link, then says the same whatever matched, announced', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage();

    await ask(user, '  test.user@example.org ');

    expect(await screen.findByText(LINK_REQUESTED)).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Check your email'})).toBeInTheDocument();
    expect(LINK_REQUESTED).toBe(
      "If the account exists and has a confirmed email address, we've sent a link to that address to reset the password. The link works once, for 30 minutes.",
    );
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await request.json()).toEqual({usernameOrEmail: 'test.user@example.org'});
    await expectAnnounced(LINK_REQUESTED);
  });

  it("says when there have been too many requests, in the API's words", async () => {
    const {user} = renderPage(() => json({title: 'Too many requests. Try again in 10 minutes.', status: 429}, 429));

    await ask(user);

    expect(await findAlert('Too many requests. Try again in 10 minutes.')).toBeInTheDocument();
    expect(screen.queryByText(LINK_REQUESTED)).not.toBeInTheDocument();
  });

  it("says when there have been too many requests when the API doesn't say how long", async () => {
    const {user} = renderPage(() => new Response(null, {status: 429}));

    await ask(user);

    expect(await findAlert('Too many requests. Wait a few minutes, then try again.')).toBeInTheDocument();
  });

  it('says when the link could not be asked for', async () => {
    const {user} = renderPage(() => json({title: 'Server error'}, 500));

    await ask(user);

    expect(await findAlert('The link could not be requested. Please try again.')).toBeInTheDocument();
  });

  it('shows a refused value on its field', async () => {
    const {user} = renderPage(() =>
      json({title: 'One or more validation errors occurred.', errors: {UsernameOrEmail: ['Enter your username or email address.']}}, 400),
    );

    await user.click(await screen.findByRole('button', {name: 'Send Reset Link'}));

    expect(await screen.findByText('Enter your username or email address.')).toBeInTheDocument();
    expect(screen.getByLabelText(/username or email address/i)).toHaveAttribute('aria-invalid', 'true');
  });

  it.each([
    ['its messages', {title: 'One or more validation errors occurred.', errors: {'': ['The request was not understood.']}}, 'The request was not understood.'],
    ['its title', {title: 'Bad request.'}, 'Bad request.'],
  ])('shows a 400 that names no field by %s', async (_, body, message) => {
    const {user} = renderPage(() => json(body, 400));

    await ask(user);

    expect(await findAlert(message)).toBeInTheDocument();
  });

  it("says it isn't available, and offers no link, when the server can't send email", async () => {
    const {fetch} = renderPage(undefined, false);

    expect(await screen.findByText(NO_EMAIL)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Send Reset Link'})).not.toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Back to sign in'})).toBeInTheDocument();
    expect(calls(fetch)).toEqual(['GET /api/auth/features']);
  });

  it('waits to know whether email can be sent before offering anything', async () => {
    mockApi({'GET /api/auth/features': () => new Promise<Response>(() => {})});
    renderWithApp(<ForgotPasswordPage />, {path: '/forgot-password'});

    expect(await screen.findByRole('heading', {name: 'Reset your password'})).toBeInTheDocument();
    expect(screen.queryByLabelText(/username or email address/i)).not.toBeInTheDocument();
    expect(screen.queryByText(NO_EMAIL)).not.toBeInTheDocument();
  });

  it('is its own page outside the signed-in layout, for someone signed out', async () => {
    mockApi({'GET /api/auth/features': () => json({canSendEmail: true}), 'GET /api/auth/whoami': () => json({isAuthenticated: false})});
    const {router} = renderApp({path: '/forgot-password'});

    expect(await screen.findByLabelText(/username or email address/i)).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toBe('/forgot-password'));
  });
});
