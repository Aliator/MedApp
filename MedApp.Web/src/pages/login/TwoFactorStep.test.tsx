import {screen, waitFor} from '@testing-library/react';
import {calls, expectAnnounced, findAlert, json, mockApi, renderWithApp, withAnnouncements} from '../../test/render';
import {CODE_STEP_ANNOUNCEMENT, LoginPage} from './LoginPage';
import {SIGN_IN_EXPIRED} from './TwoFactorStep';

/** Signing in to an account with two-factor authentication: the password, then a code. */

const CHALLENGE = 'synthetic-challenge';
const session = {sessionId: '00000000-0000-0000-0000-000000000001', expiresAtUtc: '2030-01-01T00:00:00Z'};

function renderLogin(twoFactor: Parameters<typeof mockApi>[0][string]) {
  const fetch = mockApi({
    'GET /api/auth/features': () => json({canSendEmail: true}),
    'POST /api/auth/login': () => json({challenge: CHALLENGE, expiresAtUtc: '2030-01-01T00:05:00Z'}, 202),
    'POST /api/auth/login/two-factor': twoFactor,
  });
  return {...renderWithApp(<LoginPage />, {path: '/login'}), fetch};
}

type User = ReturnType<typeof renderLogin>['user'];

/** Signs in with the password, and waits for the code step. */
async function enterPassword(user: User) {
  await user.type(await screen.findByLabelText(/username/i), 'test-admin');
  await user.type(screen.getByLabelText(/^password/i), 'Synthetic-Passw0rd');
  await user.click(screen.getByRole('button', {name: 'Sign in'}));
  return screen.findByLabelText(/code from your authenticator app/i);
}

const bodyOf = async (fetch: ReturnType<typeof mockApi>, path: string) =>
  (fetch.mock.calls.map(([r]) => r as Request).find((r) => new URL(r.url).pathname === path)!).clone().json();

describe('TwoFactorStep', () => {
  it('asks for a code after the password, focused, announced, and set up for one-time codes', async () => {
    withAnnouncements();
    const {user, router} = renderLogin(() => json(session));

    const code = await enterPassword(user);

    expect(code).toHaveFocus();
    expect(code).toHaveAttribute('autocomplete', 'one-time-code');
    expect(code).toHaveAttribute('inputmode', 'numeric');
    expect(screen.getByText('Enter the code from your authenticator app.')).toBeInTheDocument();
    expect(screen.queryByLabelText(/^password/i)).not.toBeInTheDocument();
    await expectAnnounced(CODE_STEP_ANNOUNCEMENT);
    // No session yet, so it stays on the login page.
    expect(router.state.location.pathname).toBe('/login');
  });

  it('signs in with the code, then goes to the start page', async () => {
    const {user, router, fetch} = renderLogin(() => json(session));

    await user.type(await enterPassword(user), '123456');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(await bodyOf(fetch, '/api/auth/login/two-factor')).toEqual({challenge: CHALLENGE, code: '123456'});
  });

  it('signs in with a recovery code instead, and switches back', async () => {
    const {user, router, fetch} = renderLogin(() => json(session));

    await enterPassword(user);
    await user.click(screen.getByRole('button', {name: 'Use a recovery code instead'}));
    const recovery = screen.getByLabelText(/recovery code/i);
    expect(recovery).toHaveAttribute('autocomplete', 'off');
    expect(screen.getByText(/Enter one of the recovery codes you saved/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Use a code from your authenticator app'}));
    expect(screen.getByLabelText(/code from your authenticator app/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Use a recovery code instead'}));
    await user.type(screen.getByLabelText(/recovery code/i), 'abcd-1234');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(await bodyOf(fetch, '/api/auth/login/two-factor')).toEqual({challenge: CHALLENGE, recoveryCode: 'abcd-1234'});
  });

  it("stays on the code for a wrong one, saying so in the API's words", async () => {
    const wrong = "That code isn't right. Check your authenticator app and try again.";
    const {user, router} = renderLogin(() => json({title: wrong, status: 401}, 401));

    await user.type(await enterPassword(user), '000000');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    expect(await findAlert(wrong)).toBeInTheDocument();
    expect(screen.getByLabelText(/code from your authenticator app/i)).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });

  it('goes back to the page that sent them to sign in, after the code', async () => {
    mockApi({
      'POST /api/auth/login': () => json({challenge: CHALLENGE, expiresAtUtc: '2030-01-01T00:05:00Z'}, 202),
      'POST /api/auth/login/two-factor': () => json(session),
      'GET /api/auth/whoami': () => json({isAuthenticated: true, name: 'test-admin', roles: ['Admin', 'User']}),
      'GET /api/auth/me/settings': () => json({settings: null}),
    });
    const {user, router} = renderWithApp(<LoginPage redirectTo="/confirm-email" />, {path: '/login'});

    await user.type(await enterPassword(user), '123456');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/confirm-email'));
  });

  it('stays on the code when the account has had too many codes, saying when to try again', async () => {
    const tooMany = 'Too many codes for this account. Try again in 12 minutes.';
    const {user, router} = renderLogin(() => json({title: tooMany, status: 429}, 429));

    await user.type(await enterPassword(user), '000000');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    expect(await findAlert(tooMany)).toBeInTheDocument();
    expect(screen.getByLabelText(/code from your authenticator app/i)).toHaveValue('000000');
    expect(router.state.location.pathname).toBe('/login');
  });

  it.each([
    [401, "That code isn't right. Check your authenticator app and try again."],
    [423, 'This account is locked. Try again later.'],
    [429, 'Too many sign-in attempts. Wait a minute, then try again.'],
    [500, 'Something went wrong signing in. Please try again.'],
  ])('says what a %s means when the API gives no reason', async (status, message) => {
    const {user} = renderLogin(() => new Response(null, {status}));

    await user.type(await enterPassword(user), '000000');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    expect(await findAlert(message)).toBeInTheDocument();
  });

  it.each([
    [423, 'This account is locked after too many wrong codes. Try again in 5 minutes.'],
    [429, 'Too many sign-in attempts. Try again in 42 seconds.'],
  ])("says why a %s stopped it, in the API's words", async (status, title) => {
    const {user} = renderLogin(() => json({title, status}, status));

    await user.type(await enterPassword(user), '000000');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    expect(await findAlert(title)).toBeInTheDocument();
  });

  it('goes back to the password when the sign-in has run out, saying why', async () => {
    const expired = 'This sign-in has expired. Enter your password again.';
    const {user} = renderLogin(() => json({title: expired, status: 401, code: SIGN_IN_EXPIRED}, 401));

    await user.type(await enterPassword(user), '000000');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    expect(await findAlert(expired)).toBeInTheDocument();
    expect(screen.getByLabelText(/^password/i)).toHaveValue('');
    expect(screen.getByLabelText(/username/i)).toHaveValue('test-admin');
    expect(screen.queryByLabelText(/code from your authenticator app/i)).not.toBeInTheDocument();
  });

  it("says the sign-in ran out in its own words when the API doesn't", async () => {
    const {user} = renderLogin(() => json({code: SIGN_IN_EXPIRED}, 401));

    await user.type(await enterPassword(user), '000000');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    expect(await findAlert('This sign-in has run out. Enter your password again.')).toBeInTheDocument();
  });

  it('shows a refused code on its field', async () => {
    const {user} = renderLogin(() => json({title: 'One or more validation errors occurred.', errors: {Code: ['Enter the 6-digit code.']}}, 400));

    await user.type(await enterPassword(user), '12');
    await user.click(screen.getByRole('button', {name: 'Sign in'}));

    expect(await screen.findByText('Enter the 6-digit code.')).toBeInTheDocument();
    expect(screen.getByLabelText(/code from your authenticator app/i)).toHaveAttribute('aria-invalid', 'true');
  });

  it('starts again from the password with Back, asking the API nothing more', async () => {
    const {user, fetch} = renderLogin(() => json(session));

    await enterPassword(user);
    await user.click(screen.getByRole('button', {name: 'Back'}));

    expect(await screen.findByLabelText(/^password/i)).toHaveValue('');
    // No reason given: they chose to go back.
    expect(screen.queryByText(/run out|expired|invalid credentials/i)).not.toBeInTheDocument();
    expect(calls(fetch)).not.toContain('POST /api/auth/login/two-factor');
  });
});
