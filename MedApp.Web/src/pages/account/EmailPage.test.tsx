import {screen, waitFor, within} from '@testing-library/react';
import {myEmailQuery} from '../../api/account';
import {calls, expectAnnounced, findAlert, json, mockApi, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {EMAIL_UNAVAILABLE, EmailPage} from './EmailPage';

const ME = 'GET /api/auth/me/email';
const CHANGE = 'PUT /api/auth/me/email';
const REMOVE = 'DELETE /api/auth/me/email';

function renderPage({email = {email: 'test.user@example.org', pendingEmail: null} as unknown, canSendEmail = true, extra = {}} = {}) {
  const fetch = mockApi({
    [ME]: () => json(email),
    'GET /api/auth/features': () => json({canSendEmail}),
    ...extra,
  });
  return {...renderWithApp(<EmailPage />, {path: '/account/email', user: staffUser}), fetch};
}

type User = ReturnType<typeof renderPage>['user'];

async function change(user: User, address = 'test.user.new@example.org') {
  await user.type(await screen.findByLabelText(/new email address/i), address);
  await user.click(screen.getByRole('button', {name: 'Send confirmation link'}));
}

describe('EmailPage', () => {
  it('shows the confirmed address, with a way back to Account Settings', async () => {
    renderPage();

    expect(await screen.findByText('test.user@example.org')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Email Address', level: 1})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: /back/i})).toHaveAttribute('href', '/account');
    expect(screen.getByText('Change email address')).toBeInTheDocument();
  });

  it('says an address is waiting for its link to be opened, and which stays in use until then', async () => {
    renderPage({email: {email: 'test.user@example.org', pendingEmail: 'test.user.new@example.org'}});

    expect(await screen.findByText('Waiting for you to open the link we sent to test.user.new@example.org.')).toBeInTheDocument();
    expect(screen.getByText('test.user@example.org stays in use until then.')).toBeInTheDocument();
  });

  it('says a first address waiting is used once its link is opened', async () => {
    renderPage({email: {email: null, pendingEmail: 'test.user@example.org'}});

    expect(await screen.findByText('It will be used once you open the link.')).toBeInTheDocument();
    expect(screen.getByText('None')).toBeInTheDocument();
  });

  it('invites adding one when there is none, with nothing to remove', async () => {
    renderPage({email: {}});

    expect(await screen.findByText('Add an email address')).toBeInTheDocument();
    expect(screen.getByText('None')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Remove Email Address'})).not.toBeInTheDocument();
  });

  it('sends a link to a new address, says so, and shows it waiting', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({
      extra: {[CHANGE]: () => json({email: 'test.user@example.org', pendingEmail: 'test.user.new@example.org'}, 202)},
    });

    await change(user, ' test.user.new@example.org ');

    const sent = "We've sent a link to test.user.new@example.org. Open it to confirm the address.";
    expect(await screen.findByText(sent)).toBeInTheDocument();
    expect(screen.getByText('Waiting for you to open the link we sent to test.user.new@example.org.')).toBeInTheDocument();
    expect(screen.getByLabelText(/new email address/i)).toHaveValue('');
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect(await request.json()).toEqual({email: 'test.user.new@example.org'});
    await expectAnnounced(sent);
  });

  it('names the address typed when the API sends no pending one back', async () => {
    const {user} = renderPage({extra: {[CHANGE]: () => json({}, 202)}});

    await change(user);

    expect(await screen.findByText("We've sent a link to test.user.new@example.org. Open it to confirm the address.")).toBeInTheDocument();
  });

  it('shows a refused address on its field', async () => {
    const {user} = renderPage({
      extra: {[CHANGE]: () => json({title: 'One or more validation errors occurred.', errors: {Email: ['Enter a valid email address.']}}, 400)},
    });

    await change(user, 'not-an-address');

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
    expect(screen.getByLabelText(/new email address/i)).toHaveAttribute('aria-invalid', 'true');
  });

  it.each([
    ['in its words when the API says why', () => json({title: "Email can't be sent right now.", status: 409}, 409), "Email can't be sent right now."],
    ['when it fails for another reason', () => json({title: 'Server error'}, 500), 'The link could not be sent. Please try again.'],
  ])('says when a link could not be sent, %s', async (_, respond, message) => {
    const {user} = renderPage({extra: {[CHANGE]: respond}});

    await change(user);

    expect(await findAlert(message)).toBeInTheDocument();
  });

  it('removes the address once confirmed, and says so', async () => {
    withAnnouncements();
    const {user, fetch, queryClient} = renderPage({extra: {[REMOVE]: () => new Response(null, {status: 204})}});

    await user.click(await screen.findByRole('button', {name: 'Remove Email Address'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText(/Password reset links can't reach you without one/)).toBeInTheDocument();
    expect(calls(fetch)).not.toContain(REMOVE);
    await user.click(dialog.getByRole('button', {name: 'Remove'}));

    expect(await screen.findByText('Your email address has been removed.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
    expect(queryClient.getQueryData(myEmailQuery.queryKey)).toEqual({email: null, pendingEmail: null});
    expect(screen.getByText('None')).toBeInTheDocument();
    await expectAnnounced('Email address removed');
  });

  it('keeps the address when removing is cancelled', async () => {
    const {user, fetch} = renderPage();

    await user.click(await screen.findByRole('button', {name: 'Remove Email Address'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(openDialog()).toBeNull();
    expect(calls(fetch)).not.toContain(REMOVE);
  });

  it('says when the address could not be removed', async () => {
    const {user} = renderPage({extra: {[REMOVE]: () => json({title: 'Server error'}, 500)}});

    await user.click(await screen.findByRole('button', {name: 'Remove Email Address'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Remove'}));

    expect(await findAlert('Your email address could not be removed. Please try again.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it("says email isn't available, offering no change, when the server can't send it, but still lets it be removed", async () => {
    renderPage({canSendEmail: false});

    expect(await screen.findByText(EMAIL_UNAVAILABLE)).toBeInTheDocument();
    expect(screen.queryByLabelText(/new email address/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Remove Email Address'})).toBeInTheDocument();
  });

  it('says when the address could not be loaded', async () => {
    renderPage({extra: {[ME]: () => json({title: 'Server error'}, 500)}});

    expect(await findAlert('Your email address could not be loaded. Please try again.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('None')).toBeInTheDocument());
  });
});
