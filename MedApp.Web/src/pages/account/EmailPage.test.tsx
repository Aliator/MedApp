import {screen, waitFor, within} from '@testing-library/react';
import {myEmailQuery} from '../../api/account';
import {
  calls,
  expectAnnounced,
  findAlert,
  isDisabled,
  json,
  mockApi,
  openDialog,
  renderWithApp,
  staffUser,
  withAnnouncements,
} from '../../test/render';
import {emailCodeSent} from '../../test/twoFactor';
import {CODE_FOR_CHANGE, EMAIL_UNAVAILABLE, EmailPage} from './EmailPage';

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

const PASSWORD = 'Synthetic-Passw0rd';

async function change(user: User, address = 'test.user.new@example.org', password = PASSWORD) {
  await user.type(await screen.findByLabelText(/new email address/i), address);
  await user.type(screen.getByLabelText(/your password/i), password);
  await user.click(screen.getByRole('button', {name: 'Send confirmation link'}));
}

/** Opens Remove, types the password, and confirms. */
async function removeEmail(user: User, password = PASSWORD) {
  await user.click(await screen.findByRole('button', {name: 'Remove Email Address'}));
  const dialog = within(openDialog()!);
  await user.type(dialog.getByLabelText(/your password/i), password);
  await user.click(dialog.getByRole('button', {name: 'Remove'}));
  return dialog;
}

const bodyOf = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls
    .map(([r]) => r as Request)
    .find((r) => r.method === method)!
    .clone()
    .json();

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
    expect(screen.getByLabelText(/your password/i)).toHaveValue('');
    expect(await bodyOf(fetch, 'PUT')).toEqual({email: 'test.user.new@example.org', password: PASSWORD});
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

  it('asks for the password to change it, with the username hidden beside it for password managers', async () => {
    renderPage();

    const password = await screen.findByLabelText(/your password/i);
    expect(password).toHaveAttribute('type', 'password');
    expect(password).toHaveAttribute('autocomplete', 'current-password');
    expect(document.querySelector('input[name="username"]')).toHaveValue(staffUser.username);
  });

  it('shows a wrong password on its field, and keeps the address typed', async () => {
    const {user} = renderPage({
      extra: {[CHANGE]: () => json({title: 'One or more validation errors occurred.', errors: {Password: ['Your password is incorrect.']}}, 400)},
    });

    await change(user, 'test.user.new@example.org', 'wrong-password');

    expect(await screen.findByText('Your password is incorrect.')).toBeInTheDocument();
    expect(screen.getByLabelText(/your password/i)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText(/new email address/i)).toHaveValue('test.user.new@example.org');
  });

  it.each([
    ['in its words when the API says why', () => json({title: "Email can't be sent right now.", status: 409}, 409), "Email can't be sent right now."],
    ['in its words when the account is locked', () => json({title: 'This account is locked. Try again in 5 minutes.', status: 423}, 423), 'This account is locked. Try again in 5 minutes.'],
    ['in its words after too many changes', () => json({title: 'Too many email changes. Try again in 15 minutes.', status: 429}, 429), 'Too many email changes. Try again in 15 minutes.'],
    ['when it fails for another reason', () => json({title: 'Server error'}, 500), 'The link could not be sent. Please try again.'],
  ])('says when a link could not be sent, %s', async (_, respond, message) => {
    const {user} = renderPage({extra: {[CHANGE]: respond}});

    await change(user);

    expect(await findAlert(message)).toBeInTheDocument();
  });

  it('removes the address once confirmed with the password, and says so', async () => {
    withAnnouncements();
    const {user, fetch, queryClient} = renderPage({extra: {[REMOVE]: () => new Response(null, {status: 204})}});

    await user.click(await screen.findByRole('button', {name: 'Remove Email Address'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText(/^Remove test\.user@example\.org\? Password reset links can't reach you without an email address\./)).toBeInTheDocument();
    const password = dialog.getByLabelText(/your password/i);
    expect(password).toHaveFocus();
    expect(password).toHaveAttribute('autocomplete', 'current-password');
    expect(isDisabled(dialog.getByRole('button', {name: 'Remove'}))).toBe(true);
    await user.type(password, PASSWORD);
    expect(calls(fetch)).not.toContain(REMOVE);
    await user.click(dialog.getByRole('button', {name: 'Remove'}));

    expect(await screen.findByText('Your email address has been removed.')).toBeInTheDocument();
    expect(await bodyOf(fetch, 'DELETE')).toEqual({password: PASSWORD});
    expect(openDialog()).toBeNull();
    expect(queryClient.getQueryData(myEmailQuery.queryKey)).toEqual({email: null, pendingEmail: null});
    expect(screen.getByText('None')).toBeInTheDocument();
    await expectAnnounced('Email address removed');
  });

  it('says removing also cancels a change waiting for its link', async () => {
    const {user} = renderPage({email: {email: 'test.user@example.org', pendingEmail: 'test.user.new@example.org'}});

    await user.click(await screen.findByRole('button', {name: 'Remove Email Address'}));

    expect(
      within(openDialog()!).getByText(
        /^Remove test\.user@example\.org\? This also cancels the change to test\.user\.new@example\.org, so the link we sent there stops working\./,
      ),
    ).toBeInTheDocument();
  });

  it('removes an address that is only waiting for its link, saying what that means', async () => {
    withAnnouncements();
    const {user} = renderPage({
      email: {email: null, pendingEmail: 'test.user.new@example.org'},
      extra: {[REMOVE]: () => new Response(null, {status: 204})},
    });

    await user.click(await screen.findByRole('button', {name: 'Remove Email Address'}));
    const dialog = within(openDialog()!);
    // Nothing about reset links stopping: none can reach this account yet.
    expect(dialog.getByText(/^Remove test\.user\.new@example\.org\? It's still waiting for you to open the link we sent/)).toBeInTheDocument();
    expect(dialog.queryByText(/Password reset links/)).not.toBeInTheDocument();
    await user.type(dialog.getByLabelText(/your password/i), PASSWORD);
    await user.click(dialog.getByRole('button', {name: 'Remove'}));

    expect(await screen.findByText('test.user.new@example.org has been removed, and the link we sent there no longer works.')).toBeInTheDocument();
    await expectAnnounced('Email address removed');
  });

  it('keeps the address when removing is cancelled', async () => {
    const {user, fetch} = renderPage();

    await user.click(await screen.findByRole('button', {name: 'Remove Email Address'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(openDialog()).toBeNull();
    expect(calls(fetch)).not.toContain(REMOVE);
  });

  it('shows a wrong password on its field in the dialog, and anything else above it', async () => {
    const {user} = renderPage({
      extra: {
        [REMOVE]: () =>
          json({title: 'One or more validation errors occurred.', errors: {Password: ['Your password is incorrect.'], '': ['Something else.']}}, 400),
      },
    });

    const dialog = await removeEmail(user, 'wrong-password');

    expect((await dialog.findAllByText('Your password is incorrect.')).length).toBeGreaterThan(0);
    expect(dialog.getByLabelText(/your password/i)).toHaveAttribute('aria-invalid', 'true');
    expect(dialog.getByText('Something else.')).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
  });

  it.each([
    ['locked', () => json({title: 'This account is locked. Try again in 5 minutes.', status: 423}, 423), 'This account is locked. Try again in 5 minutes.'],
    [
      'it signs in with codes by email',
      () => json({title: 'You sign in with codes sent to this address. Turn off two-factor authentication first.', status: 409}, 409),
      'You sign in with codes sent to this address. Turn off two-factor authentication first.',
    ],
    ['a server error', () => json({title: 'Server error'}, 500), 'Your email address could not be removed. Please try again.'],
  ])('says in the dialog when the address could not be removed: %s', async (_, respond, message) => {
    const {user} = renderPage({extra: {[REMOVE]: respond}});

    const dialog = await removeEmail(user);

    expect(await dialog.findByText(message)).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
    expect(screen.getByText('test.user@example.org')).toBeInTheDocument();
  });

  it('starts the dialog afresh each time it opens', async () => {
    const {user} = renderPage({extra: {[REMOVE]: () => json({title: 'Server error'}, 500)}});

    const dialog = await removeEmail(user);
    expect(await dialog.findByText('Your email address could not be removed. Please try again.')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    await user.click(screen.getByRole('button', {name: 'Remove Email Address'}));

    const again = within(openDialog()!);
    expect(again.getByLabelText(/your password/i)).toHaveValue('');
    expect(again.queryByText('Your email address could not be removed. Please try again.')).not.toBeInTheDocument();
  });

  it("says email isn't available, offering no change, when the server can't send it, but still lets it be removed", async () => {
    renderPage({canSendEmail: false});

    expect(await screen.findByText(EMAIL_UNAVAILABLE)).toBeInTheDocument();
    expect(screen.queryByLabelText(/new email address/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Remove Email Address'})).toBeInTheDocument();
    expect(screen.getByText('Change email address')).toBeInTheDocument();
  });

  it("names the unavailable card for adding when there's no address", async () => {
    renderPage({canSendEmail: false, email: {}});

    expect(await screen.findByText(EMAIL_UNAVAILABLE)).toBeInTheDocument();
    expect(screen.getByText('Add an email address')).toBeInTheDocument();
    expect(screen.queryByText('Change email address')).not.toBeInTheDocument();
  });

  it('says when the address could not be loaded', async () => {
    renderPage({extra: {[ME]: () => json({title: 'Server error'}, 500)}});

    expect(await findAlert('Your email address could not be loaded. Please try again.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('None')).toBeInTheDocument());
  });
});

describe('EmailPage, while the account signs in with codes by email', () => {
  const STATUS = 'GET /api/auth/me/two-factor';
  const SEND_CODE = 'POST /api/auth/me/two-factor/email/code';
  const usingEmailCodes = {enabled: true, method: 'Email', recoveryCodesLeft: 10, required: false, canUseEmailCodes: true};

  it('asks for a code too, emailed after the password or a recovery code, and sends it with the change', async () => {
    const {user, fetch} = renderPage({
      extra: {
        [STATUS]: () => json(usingEmailCodes),
        [SEND_CODE]: () => json(emailCodeSent, 202),
        [CHANGE]: () => json({email: 'test.user@example.org', pendingEmail: 'test.user.new@example.org'}, 202),
      },
    });

    expect(await screen.findByText(CODE_FOR_CHANGE)).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Email me a code'}))).toBe(true);
    await user.type(screen.getByLabelText(/new email address/i), 'test.user.new@example.org');
    await user.type(screen.getByLabelText(/your password/i), PASSWORD);
    await user.click(screen.getByRole('button', {name: 'Email me a code'}));
    expect(await screen.findByText(`We emailed a code to ${emailCodeSent.sentTo}.`, {exact: false})).toBeInTheDocument();
    await user.type(screen.getByLabelText(/code from the email, or a recovery code/i), '654321');
    await user.click(screen.getByRole('button', {name: 'Send confirmation link'}));

    expect(await screen.findByText(/We've sent a link to test.user.new@example.org/)).toBeInTheDocument();
    expect(await bodyOf(fetch, 'PUT')).toEqual({email: 'test.user.new@example.org', password: PASSWORD, code: '654321'});
    // Many steps, typed one key at a time: more than the default 5 seconds on a busy machine.
  }, 15_000);

  it('shows a wrong code on its field', async () => {
    const {user} = renderPage({
      extra: {
        [STATUS]: () => json(usingEmailCodes),
        [CHANGE]: () =>
          json({title: 'One or more validation errors occurred.', status: 400, errors: {Code: ["That code isn't right."]}}, 400),
      },
    });

    await user.type(await screen.findByLabelText(/new email address/i), 'test.user.new@example.org');
    await user.type(screen.getByLabelText(/your password/i), PASSWORD);
    await user.type(await screen.findByLabelText(/code from the email, or a recovery code/i), 'ABCDE-23456');
    await user.click(screen.getByRole('button', {name: 'Send confirmation link'}));

    await waitFor(() =>
      expect(screen.getByLabelText(/code from the email, or a recovery code/i)).toHaveAttribute('aria-invalid', 'true'),
    );
  });

  it('shows a wrong password from sending the code on the password field', async () => {
    const {user} = renderPage({
      extra: {
        [STATUS]: () => json(usingEmailCodes),
        [SEND_CODE]: () =>
          json({title: 'One or more validation errors occurred.', status: 400, errors: {Password: ['That password is wrong.']}}, 400),
      },
    });

    await user.type(await screen.findByLabelText(/your password/i), 'not-it');
    await user.click(await screen.findByRole('button', {name: 'Email me a code'}));

    await waitFor(() => expect(screen.getByLabelText(/your password/i)).toHaveAttribute('aria-invalid', 'true'));
  });

  const CODE_NEEDED = "Enter the code we emailed you, or one of your recovery codes, to confirm it's you.";
  const codeNeeded = () =>
    json({title: 'One or more validation errors occurred.', status: 400, errors: {Code: [CODE_NEEDED]}}, 400);

  it("says a code is needed in the banner when its field isn't showing, and checks the status again", async () => {
    // The status says the app (out of date: codes by email were turned on elsewhere), so there's no code field.
    const {user, fetch} = renderPage({
      extra: {[STATUS]: () => json({...usingEmailCodes, method: 'Authenticator'}), [CHANGE]: codeNeeded},
    });
    await screen.findByLabelText(/new email address/i);
    await waitFor(() => expect(calls(fetch).filter((c) => c === STATUS)).toHaveLength(1));

    await change(user);

    expect(await findAlert(CODE_NEEDED)).toBeInTheDocument();
    await waitFor(() => expect(calls(fetch).filter((c) => c === STATUS)).toHaveLength(2));
  });

  it('shows the code field once the status, checked again, says codes by email', async () => {
    let statusReads = 0;
    const {user} = renderPage({
      extra: {
        [STATUS]: () => json(statusReads++ === 0 ? {...usingEmailCodes, method: 'Authenticator'} : usingEmailCodes),
        [CHANGE]: codeNeeded,
      },
    });
    await screen.findByLabelText(/new email address/i);
    await waitFor(() => expect(statusReads).toBe(1));
    expect(screen.queryByText(CODE_FOR_CHANGE)).not.toBeInTheDocument();

    await change(user);

    expect(await screen.findByText(CODE_FOR_CHANGE)).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByLabelText(/code from the email, or a recovery code/i)).toHaveAttribute('aria-invalid', 'true'),
    );
  });

  it("asks for no code when the account doesn't use codes by email", async () => {
    const {user, fetch} = renderPage({
      extra: {
        [STATUS]: () => json({...usingEmailCodes, method: 'Authenticator'}),
        [CHANGE]: () => json({email: 'test.user@example.org', pendingEmail: 'test.user.new@example.org'}, 202),
      },
    });

    await change(user);

    expect(await screen.findByText(/We've sent a link/)).toBeInTheDocument();
    expect(screen.queryByText(CODE_FOR_CHANGE)).not.toBeInTheDocument();
    expect(await bodyOf(fetch, 'PUT')).toEqual({email: 'test.user.new@example.org', password: PASSWORD});
  });
});
