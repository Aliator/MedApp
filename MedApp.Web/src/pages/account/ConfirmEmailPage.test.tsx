import {screen} from '@testing-library/react';
import {myEmailQuery} from '../../api/account';
import {expectAnnounced, findAlert, json, mockApi, renderWithApp, withAnnouncements} from '../../test/render';
import {BROKEN_EMAIL_LINK, ConfirmEmailPage, EMAIL_CONFIRMED} from './ConfirmEmailPage';

const USER = '0000000e-0000-0000-0000-000000000003';

function renderPage(confirm: Parameters<typeof mockApi>[0][string] = () => new Response(null, {status: 204}), link = `/confirm-email#user=${USER}&token=synthetic%2Btoken`) {
  window.history.replaceState(null, '', link);
  const fetch = mockApi({'POST /api/auth/email/confirm': confirm});
  return {...renderWithApp(<ConfirmEmailPage />, {path: '/confirm-email'}), fetch};
}

describe('ConfirmEmailPage', () => {
  afterEach(() => window.history.replaceState(null, '', '/'));

  it("waits for a click, so a mail scanner opening the link doesn't confirm it", async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('button', {name: 'Confirm email address'})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Confirm your email address', level: 1})).toBeInTheDocument();
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
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({userId: USER, token: 'synthetic+token'});
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
    const {fetch} = renderPage(undefined, '/confirm-email');

    expect(await findAlert(BROKEN_EMAIL_LINK)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Confirm email address'})).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });
});
