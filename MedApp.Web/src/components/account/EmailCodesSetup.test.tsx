import {screen, waitFor} from '@testing-library/react';
import {adminUser, calls, expectAnnounced, findAlert, isDisabled, json, mockApi, renderWithApp, withAnnouncements} from '../../test/render';
import {emailCodeSent, recoveryCodes} from '../../test/twoFactor';
import {EmailCodesSetup, SETUP_NO_MORE_CODES} from './EmailCodesSetup';

const SEND = 'POST /api/auth/me/two-factor/email';
const ENABLE = 'POST /api/auth/me/two-factor/email/enable';

function renderSetup(handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    [SEND]: () => json(emailCodeSent, 202),
    [ENABLE]: () => json({recoveryCodes}),
    ...handlers,
  });
  const onDone = vi.fn();
  const onEnabled = vi.fn();
  const onCancel = vi.fn();
  const result = renderWithApp(
    <EmailCodesSetup username="test-admin" doneLabel="Done" onDone={onDone} onEnabled={onEnabled} onCancel={onCancel} />,
    {user: adminUser},
  );
  return {...result, fetch, onDone, onEnabled, onCancel};
}

type User = ReturnType<typeof renderSetup>['user'];

const bodyOf = (fetch: ReturnType<typeof mockApi>, path: string, index = 0) =>
  fetch.mock.calls
    .map(([r]) => r as Request)
    .filter((r) => new URL(r.url).pathname === path)
    .at(index)!
    .clone()
    .json();

async function sendCode(user: User, password = 'Synthetic-Passw0rd') {
  await user.type(await screen.findByLabelText(/your password/i), password);
  await user.click(screen.getByRole('button', {name: 'Email Me a Code'}));
}

describe('EmailCodesSetup', () => {
  it('asks for the password first, with the username hidden beside it for password managers', async () => {
    const {fetch} = renderSetup();

    const password = await screen.findByLabelText(/your password/i);
    expect(password).toHaveAttribute('autocomplete', 'current-password');
    expect(password).toHaveFocus();
    expect(document.querySelector('input[name="username"]')).toHaveValue('test-admin');
    expect(isDisabled(screen.getByRole('button', {name: 'Email Me a Code'}))).toBe(true);
    // Nothing is sent until they ask.
    expect(calls(fetch)).toEqual([]);
  });

  it('emails a code, then turns codes by email on with it and shows the recovery codes once, until Done', async () => {
    withAnnouncements();
    const {user, fetch, onDone, onEnabled} = renderSetup();

    await sendCode(user);
    expect(await screen.findByText(`We emailed a code to ${emailCodeSent.sentTo}.`, {exact: false})).toBeInTheDocument();
    expect(await bodyOf(fetch, '/api/auth/me/two-factor/email')).toEqual({password: 'Synthetic-Passw0rd'});
    const code = screen.getByLabelText(/code from the email/i);
    expect(code).toHaveAttribute('inputmode', 'numeric');
    await user.type(code, '123456');
    await user.click(screen.getByRole('button', {name: 'Turn On Codes by Email'}));

    expect(await screen.findByText(recoveryCodes[0])).toBeInTheDocument();
    expect(await bodyOf(fetch, '/api/auth/me/two-factor/email/enable')).toEqual({code: '123456'});
    expect(onEnabled).toHaveBeenCalledOnce();
    await expectAnnounced('Codes by email are on.');
    await user.click(screen.getByRole('button', {name: 'Done'}));
    expect(onDone).toHaveBeenCalledOnce();
  });

  it('sends a new code with the same password when asked, and says so', async () => {
    withAnnouncements();
    const {user, fetch} = renderSetup({[SEND]: () => json({...emailCodeSent, resendInSeconds: 0, resendsLeft: 2}, 202)});

    await sendCode(user);
    await user.click(await screen.findByRole('button', {name: 'Resend code'}));

    await waitFor(() => expect(calls(fetch).filter((c) => c === SEND)).toHaveLength(2));
    expect(await bodyOf(fetch, '/api/auth/me/two-factor/email', 1)).toEqual({password: 'Synthetic-Passw0rd'});
    await expectAnnounced(`A new code was emailed to ${emailCodeSent.sentTo}.`);
  });

  it('says when no more codes can be sent for now', async () => {
    const {user} = renderSetup({[SEND]: () => json({...emailCodeSent, resendsLeft: 0}, 202)});

    await sendCode(user);

    expect(await screen.findByText(SETUP_NO_MORE_CODES)).toBeInTheDocument();
  });

  it('shows a wrong password on its field', async () => {
    const {user} = renderSetup({
      [SEND]: () => json({title: 'One or more validation errors occurred.', errors: {Password: ['Your password is incorrect.']}}, 400),
    });

    await sendCode(user, 'wrong');

    expect((await screen.findAllByText('Your password is incorrect.')).length).toBeGreaterThan(0);
    expect(screen.getByLabelText(/your password/i)).toHaveAttribute('aria-invalid', 'true');
  });

  it.each([
    [409, 'Confirm an email address before turning on codes by email.'],
    [423, 'This account is locked. Try again in 5 minutes.'],
    [429, 'Too many codes for this account. Try again in 12 minutes.'],
  ])("says why a %s refused sending a code, in the API's words", async (status, title) => {
    const {user} = renderSetup({[SEND]: () => json({title, status}, status)});

    await sendCode(user);

    expect(await findAlert(title)).toBeInTheDocument();
  });

  it("says why a resend was refused, in the API's words, keeping the code field", async () => {
    let sends = 0;
    const tooSoon = 'You can ask for another code in 42 seconds.';
    const {user} = renderSetup({
      [SEND]: () => (++sends === 1 ? json({...emailCodeSent, resendInSeconds: 0}, 202) : json({title: tooSoon, status: 429}, 429)),
    });

    await sendCode(user);
    await user.click(await screen.findByRole('button', {name: 'Resend code'}));

    expect(await findAlert(tooSoon)).toBeInTheDocument();
    expect(screen.getByLabelText(/code from the email/i)).toBeInTheDocument();
  });

  it('shows a wrong code on its field, and anything else above it', async () => {
    const {user} = renderSetup({
      [ENABLE]: () =>
        json({title: 'One or more validation errors occurred.', errors: {Code: ["That code isn't right."], '': ['Something else.']}}, 400),
    });

    await sendCode(user);
    await user.type(await screen.findByLabelText(/code from the email/i), '000000');
    await user.click(screen.getByRole('button', {name: 'Turn On Codes by Email'}));

    expect((await screen.findAllByText("That code isn't right.")).length).toBeGreaterThan(0);
    expect(screen.getByLabelText(/code from the email/i)).toHaveAttribute('aria-invalid', 'true');
    expect(await findAlert('Something else.')).toBeInTheDocument();
  });

  it("says why turning it on was refused, in the API's words", async () => {
    const {user} = renderSetup({[ENABLE]: () => json({title: 'Two-factor authentication is already on.', status: 409}, 409)});

    await sendCode(user);
    await user.type(await screen.findByLabelText(/code from the email/i), '123456');
    await user.click(screen.getByRole('button', {name: 'Turn On Codes by Email'}));

    expect(await findAlert('Two-factor authentication is already on.')).toBeInTheDocument();
  });

  it('can be cancelled at either step', async () => {
    const {user, onCancel} = renderSetup();

    await user.click(await screen.findByRole('button', {name: 'Cancel'}));
    await sendCode(user);
    await screen.findByLabelText(/code from the email/i);
    await user.click(screen.getByRole('button', {name: 'Cancel'}));

    expect(onCancel).toHaveBeenCalledTimes(2);
  });
});
