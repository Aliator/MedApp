import {screen, waitFor} from '@testing-library/react';
import {myEmailQuery} from '../../api/account';
import {PENDING_CONFIRMATION_KEY, pendingEmailConfirmation} from '../../auth/emailLink';
import {expectAnnounced, findAlert, json, mockApi, renderWithApp, signedOutUser, staffUser, withAnnouncements} from '../../test/render';
import {BROKEN_EMAIL_LINK, ConfirmEmailPage, EMAIL_CONFIRMED, SIGN_IN_TO_CONFIRM} from './ConfirmEmailPage';

const USER = '0000000e-0000-0000-0000-000000000003';
const LINK = `/confirm-email#user=${USER}&token=synthetic%2Btoken`;
const CONFIRM = 'POST /api/auth/email/confirm';

function renderPage(
  confirm: Parameters<typeof mockApi>[0][string] = () => new Response(null, {status: 204}),
  {link = LINK, user = staffUser, extra = {}}: {link?: string; user?: typeof staffUser; extra?: Parameters<typeof mockApi>[0]} = {},
) {
  window.history.replaceState(null, '', link);
  const fetch = mockApi({[CONFIRM]: confirm, ...extra});
  return {...renderWithApp(<ConfirmEmailPage />, {path: '/confirm-email', user}), fetch};
}

const confirmCalls = (fetch: ReturnType<typeof mockApi>) =>
  fetch.mock.calls.filter(([r]) => new URL((r as Request).url).pathname === '/api/auth/email/confirm');

describe('ConfirmEmailPage', () => {
  afterEach(() => {
    window.history.replaceState(null, '', '/');
    sessionStorage.clear();
  });

  it("waits for a click, so a mail scanner opening the link doesn't confirm it", async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('button', {name: 'Confirm email address'})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Confirm your email address', level: 1})).toBeInTheDocument();
    expect(screen.getByText(/You're\s+signed in as test-user\./)).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
    expect(window.location.hash).toBe('');
  });

  it("confirms with the link's token, says so, and refreshes the account's email", async () => {
    withAnnouncements();
    const {user, fetch, queryClient} = renderPage();
    queryClient.setQueryData(myEmailQuery.queryKey, {email: null, pendingEmail: 'test.user@example.org'});

    await user.click(await screen.findByRole('button', {name: 'Confirm email address'}));

    expect(await screen.findByText(EMAIL_CONFIRMED)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Continue to MedApp'})).toHaveAttribute('href', '/');
    expect(await (confirmCalls(fetch)[0][0] as Request).json()).toEqual({userId: USER, token: 'synthetic+token'});
    expect(queryClient.getQueryState(myEmailQuery.queryKey)?.isInvalidated).toBe(true);
    await expectAnnounced(EMAIL_CONFIRMED);
  });

  it("says why a link can't be used, in the API's words, with what to do", async () => {
    const {user} = renderPage(() => json([{code: 'InvalidToken', description: 'This link has expired or has already been used.'}], 400));

    await user.click(await screen.findByRole('button', {name: 'Confirm email address'}));

    expect(await findAlert('This link has expired or has already been used.')).toBeInTheDocument();
    expect(screen.getByText(/To get a new link, change your email address again/)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Confirm email address'})).not.toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Continue to MedApp'})).toBeInTheDocument();
  });

  it('shows the title of a 400 with nothing else to say', async () => {
    const {user} = renderPage(() => json({title: 'Bad request.'}, 400));

    await user.click(await screen.findByRole('button', {name: 'Confirm email address'}));

    expect(await findAlert('Bad request.')).toBeInTheDocument();
  });

  it('says when it could not be confirmed for another reason, and lets them try again', async () => {
    const {user} = renderPage(() => json({title: 'Server error'}, 500));

    await user.click(await screen.findByRole('button', {name: 'Confirm email address'}));

    expect(await findAlert('The email address could not be confirmed. Please try again.')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Confirm email address'})).toBeInTheDocument();
  });

  it("says a link that isn't whole can't be used", async () => {
    const {fetch} = renderPage(undefined, {link: '/confirm-email'});

    expect(await findAlert(BROKEN_EMAIL_LINK)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Confirm email address'})).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  describe('signed out', () => {
    it('asks them to sign in, confirming nothing yet', async () => {
      const {fetch} = renderPage(undefined, {user: signedOutUser});

      expect(await screen.findByText(SIGN_IN_TO_CONFIRM)).toBeInTheDocument();
      expect(screen.getByText(/Sign in with the account this address was given for/)).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: 'Confirm email address'})).not.toBeInTheDocument();
      expect(confirmCalls(fetch)).toHaveLength(0);
      expect(window.location.hash).toBe('');
    });

    it('keeps the link in this tab and sends them to sign in, to come back here', async () => {
      const {user, router} = renderPage(undefined, {user: signedOutUser});

      await user.click(await screen.findByRole('button', {name: 'Sign in'}));

      await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
      expect(router.state.location.search).toEqual({redirect: '/confirm-email'});
      expect(pendingEmailConfirmation.read()).toEqual({userId: USER, token: 'synthetic+token'});
      expect(localStorage.getItem(PENDING_CONFIRMATION_KEY)).toBeNull();
    });

    it('asks who is signed in when the cache does not know', async () => {
      window.history.replaceState(null, '', LINK);
      mockApi({'GET /api/auth/whoami': () => json({isAuthenticated: false})});
      renderWithApp(<ConfirmEmailPage />, {path: '/confirm-email'});

      expect(await screen.findByText(SIGN_IN_TO_CONFIRM)).toBeInTheDocument();
    });
  });

  describe('back from signing in', () => {
    it('confirms the kept link with a click, then forgets it', async () => {
      pendingEmailConfirmation.save({userId: USER, token: 'kept-token'});
      const {user, fetch} = renderPage(undefined, {link: '/confirm-email'});

      await user.click(await screen.findByRole('button', {name: 'Confirm email address'}));

      expect(await screen.findByText(EMAIL_CONFIRMED)).toBeInTheDocument();
      expect(await (confirmCalls(fetch)[0][0] as Request).json()).toEqual({userId: USER, token: 'kept-token'});
      expect(sessionStorage.getItem(PENDING_CONFIRMATION_KEY)).toBeNull();
    });

    it('forgets the kept link when it is refused too', async () => {
      pendingEmailConfirmation.save({userId: USER, token: 'kept-token'});
      const {user} = renderPage(() => json([{code: 'InvalidToken', description: 'This link has expired or has already been used.'}], 400), {
        link: '/confirm-email',
      });

      await user.click(await screen.findByRole('button', {name: 'Confirm email address'}));

      expect(await findAlert('This link has expired or has already been used.')).toBeInTheDocument();
      expect(sessionStorage.getItem(PENDING_CONFIRMATION_KEY)).toBeNull();
    });
  });

  describe('signed in as another account', () => {
    const wrongAccount = 'This link is for a different account. Sign in with the account it was sent for.';

    it('says so, with a way to sign out, and no way to confirm', async () => {
      const {user} = renderPage(() => json([{code: 'WrongAccount', description: wrongAccount}], 400));

      await user.click(await screen.findByRole('button', {name: 'Confirm email address'}));

      expect(await findAlert(wrongAccount)).toBeInTheDocument();
      expect(screen.getByText(/Sign out, then open the link from the email again/)).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: 'Confirm email address'})).not.toBeInTheDocument();
      expect(screen.getByRole('link', {name: 'Continue to MedApp'})).toBeInTheDocument();
    });

    it('signs out and goes to sign in', async () => {
      const {user, router, fetch} = renderPage(() => json([{code: 'WrongAccount', description: wrongAccount}], 400), {
        extra: {'POST /api/auth/logout': () => new Response(null, {status: 204})},
      });

      await user.click(await screen.findByRole('button', {name: 'Confirm email address'}));
      await user.click(await screen.findByRole('button', {name: 'Sign out'}));

      await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
      expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname === '/api/auth/logout')).toBe(true);
    });
  });

  it('asks them to sign in again when the session ended meanwhile (a 401)', async () => {
    let signedIn = true;
    const {user} = renderPage(
      () => {
        signedIn = false;
        return json({title: 'Unauthorized', status: 401}, 401);
      },
      {extra: {'GET /api/auth/whoami': () => json(signedIn ? {isAuthenticated: true, name: 'test-user', roles: ['User']} : {isAuthenticated: false})}},
    );

    await user.click(await screen.findByRole('button', {name: 'Confirm email address'}));

    expect(await screen.findByText(SIGN_IN_TO_CONFIRM)).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Sign in'})).toBeInTheDocument();
  });
});
