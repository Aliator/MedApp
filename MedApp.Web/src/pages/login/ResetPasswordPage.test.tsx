import {screen} from '@testing-library/react';
import {whoamiQuery} from '../../auth/auth';
import {expectAnnounced, findAlert, json, mockApi, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {BROKEN_LINK, PASSWORD_RESET, ResetPasswordPage} from './ResetPasswordPage';

const USER = '0000000e-0000-0000-0000-000000000002';
const LINK = `/reset-password#user=${USER}&token=synthetic%2Btoken`;

function renderPage(reset: Parameters<typeof mockApi>[0][string] = () => new Response(null, {status: 204}), link = LINK) {
  window.history.replaceState(null, '', link);
  const fetch = mockApi({'POST /api/auth/password/reset': reset});
  return {...renderWithApp(<ResetPasswordPage />, {path: '/reset-password', user: staffUser}), fetch};
}

type User = ReturnType<typeof renderPage>['user'];

async function choose(user: User, next = 'Chosen-Synthetic1', confirm = next) {
  if (next) await user.type(await screen.findByLabelText(/^new password/i), next);
  if (confirm) await user.type(screen.getByLabelText(/^confirm new password/i), confirm);
  await user.click(screen.getByRole('button', {name: 'Change Password'}));
}

describe('ResetPasswordPage', () => {
  afterEach(() => window.history.replaceState(null, '', '/'));

  it('asks for a new password twice, as password managers expect, and takes the token off the address', async () => {
    renderPage();

    expect(await screen.findByRole('heading', {name: 'Choose a new password', level: 1})).toBeInTheDocument();
    expect(await screen.findByLabelText(/^new password/i)).toHaveAttribute('autocomplete', 'new-password');
    expect(screen.getByLabelText(/^confirm new password/i)).toHaveAttribute('autocomplete', 'new-password');
    expect(window.location.hash).toBe('');
    expect(document.title).toBe('Choose a new password - MedApp');
  });

  it('reads the link as the app runs in development (StrictMode, which runs effects twice), and resets with it', async () => {
    window.history.replaceState(null, '', LINK);
    const fetch = mockApi({'POST /api/auth/password/reset': () => new Response(null, {status: 204})});
    const {user} = renderWithApp(<ResetPasswordPage />, {path: '/reset-password', user: staffUser, strict: true});

    await choose(user);

    expect(await screen.findByText(PASSWORD_RESET)).toBeInTheDocument();
    expect(screen.queryByText(BROKEN_LINK)).not.toBeInTheDocument();
    expect(await (fetch.mock.calls[0][0] as Request).json()).toMatchObject({userId: USER, token: 'synthetic+token'});
  });

  it("changes the password with the link's token, then says they're signed out everywhere and offers to sign in", async () => {
    withAnnouncements();
    const {user, fetch, queryClient} = renderPage();

    await choose(user);

    expect(await screen.findByText(PASSWORD_RESET)).toBeInTheDocument();
    expect(PASSWORD_RESET).toBe("Your password has been changed and you've been signed out everywhere. Sign in with your new password.");
    expect(screen.getByRole('link', {name: 'Sign In'})).toHaveAttribute('href', '/login');
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({userId: USER, token: 'synthetic+token', newPassword: 'Chosen-Synthetic1'});
    // Whoever this browser had signed in is signed out now.
    expect(queryClient.getQueryData(whoamiQuery.queryKey)).toBeUndefined();
    await expectAnnounced(PASSWORD_RESET);
  });

  it('checks the passwords match, and that one was given, before asking the API', async () => {
    const {user, fetch} = renderPage();

    await choose(user, 'Chosen-Synthetic1', 'Different-Synthetic1');
    expect(await findAlert('New passwords do not match.')).toBeInTheDocument();
    await user.clear(screen.getByLabelText(/^new password/i));
    await user.clear(screen.getByLabelText(/^confirm new password/i));
    await choose(user, '', '');
    expect(await findAlert('Please enter a new password.')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it("shows Identity's reasons for refusing the password on its field, and stays", async () => {
    const {user} = renderPage(() =>
      json(
        [
          {code: 'PasswordTooShort', description: 'Passwords must be at least 12 characters.'},
          {code: 'SamePassword', description: 'Choose a password you have not used here.'},
        ],
        400,
      ),
    );

    await choose(user, 'short', 'short');

    expect(await screen.findByText('Passwords must be at least 12 characters. Choose a password you have not used here.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^new password/i)).toHaveAttribute('aria-invalid', 'true');
  });

  it("shows validation's reason for refusing the password on its field", async () => {
    const {user} = renderPage(() => json({title: 'One or more validation errors occurred.', errors: {NewPassword: ['Too long.']}}, 400));

    await choose(user);

    expect(await screen.findByText('Too long.')).toBeInTheDocument();
  });

  it('says a link that has run out or been used can\'t be, offering a new one', async () => {
    const expired = 'This link has expired or has already been used.';
    const {user} = renderPage(() => json([{code: 'InvalidToken', description: expired}], 400));

    await choose(user);

    expect(await findAlert(expired)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Ask for a New Link'})).toHaveAttribute('href', '/forgot-password');
    expect(screen.queryByLabelText(/^new password/i)).not.toBeInTheDocument();
  });

  it("says a link whose parts validation refuses isn't complete", async () => {
    const {user} = renderPage(() => json({title: 'One or more validation errors occurred.', errors: {Token: ['Token is required.']}}, 400));

    await choose(user);

    expect(await findAlert(BROKEN_LINK)).toBeInTheDocument();
  });

  it.each([
    ['too many attempts', () => json({title: 'Too many attempts. Try again in 10 minutes.', status: 429}, 429), 'Too many attempts. Try again in 10 minutes.'],
    ['too many attempts, without a reason', () => new Response(null, {status: 429}), 'Too many attempts. Wait a few minutes, then try again.'],
    ['a server error', () => json({title: 'Server error'}, 500), 'The password could not be changed. Please try again.'],
    ['a 400 that names nothing', () => json({title: 'Bad request.'}, 400), 'Bad request.'],
  ])('says so for %s', async (_, respond, message) => {
    const {user} = renderPage(respond);

    await choose(user);

    expect(await findAlert(message)).toBeInTheDocument();
  });

  it('says a link without its token is not complete', async () => {
    const {fetch} = renderPage(undefined, `/reset-password#user=${USER}`);

    expect(await findAlert(BROKEN_LINK)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Back to sign in'})).toHaveAttribute('href', '/login');
    expect(fetch).not.toHaveBeenCalled();
  });
});
