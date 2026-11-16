import {act, screen, waitFor, within} from '@testing-library/react';
import {twoFactorStatusQuery} from '../../api/account';
import {adminUser, calls, expectAnnounced, findAlert, isDisabled, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {authenticatorSetup, recoveryCodes} from '../../test/twoFactor';
import {REQUIRED, SecurityPage} from './SecurityPage';

const STATUS = 'GET /api/auth/me/two-factor';
const DISABLE = 'POST /api/auth/me/two-factor/disable';
const CODES = 'POST /api/auth/me/two-factor/recovery-codes';

const on = {enabled: true, recoveryCodesLeft: 8, required: false};
const off = {enabled: false, recoveryCodesLeft: 0, required: false};

function renderPage(status: unknown = on, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[STATUS]: () => json(status), ...extra});
  return {...renderWithApp(<SecurityPage />, {path: '/account/security', user: adminUser}), fetch};
}

type User = ReturnType<typeof renderPage>['user'];

const bodyOf = (fetch: ReturnType<typeof mockApi>, method: string, path: string) =>
  fetch.mock.calls
    .map(([r]) => r as Request)
    .find((r) => r.method === method && new URL(r.url).pathname === path)!
    .clone()
    .json();

async function turnOff(user: User, password = 'Synthetic-Passw0rd', code = '123456') {
  await user.click(await screen.findByRole('button', {name: 'Turn Off'}));
  const dialog = within(openDialog()!);
  if (password) await user.type(dialog.getByLabelText(/your password/i), password);
  if (code) await user.type(dialog.getByLabelText(/authenticator code or recovery code/i), code);
  await user.click(dialog.getByRole('button', {name: 'Turn Off'}));
  return dialog;
}

async function getNewCodes(user: User, code = '123456', password = 'Synthetic-Passw0rd') {
  await user.click(await screen.findByRole('button', {name: 'New Recovery Codes'}));
  const dialog = within(openDialog()!);
  await user.type(dialog.getByLabelText(/your password/i), password);
  await user.type(dialog.getByLabelText(/code from your authenticator app/i), code);
  await user.click(dialog.getByRole('button', {name: 'Get New Codes'}));
  return dialog;
}

describe('SecurityPage', () => {
  it('shows two-factor on, with the recovery codes left', async () => {
    renderPage();

    expect(await screen.findByText('On')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Security', level: 1})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: /back/i})).toHaveAttribute('href', '/account');
    expect(screen.queryByText(REQUIRED)).not.toBeInTheDocument();
  });

  it.each([
    [0, 'You have no recovery codes left.'],
    [1, 'You have only 1 recovery code left.'],
    [3, 'You have only 3 recovery codes left.'],
  ])('warns when only %s recovery codes are left', async (left, message) => {
    renderPage({...on, recoveryCodesLeft: left});

    expect(await screen.findByText(message)).toBeInTheDocument();
  });

  it("can't be turned off when the account must use it, and says why", async () => {
    renderPage({...on, required: true});

    // On the page, and as the button's tooltip.
    expect((await screen.findAllByText(REQUIRED)).length).toBeGreaterThan(0);
    expect(isDisabled(screen.getByRole('button', {name: 'Turn Off'}))).toBe(true);
  });

  it('sets it up from off, then shows it on', async () => {
    let status = off;
    const {user} = renderPage(undefined, {
      [STATUS]: () => json(status),
      'POST /api/auth/me/two-factor/authenticator': () => json(authenticatorSetup),
      'POST /api/auth/me/two-factor/enable': () => {
        status = {...on, recoveryCodesLeft: 10};
        return json({recoveryCodes});
      },
    });

    expect(await screen.findByText('Off')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Set Up Two-Factor'}));
    await user.type(await screen.findByLabelText(/your password/i), 'Synthetic-Passw0rd');
    await user.type(screen.getByLabelText(/code from your authenticator app/i), '123456');
    await user.click(screen.getByRole('button', {name: 'Turn on two-factor'}));
    expect(await screen.findByText(recoveryCodes[0])).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Done'}));

    expect(await screen.findByText('On')).toBeInTheDocument();
    expect(screen.getByText('10')).toBeInTheDocument();
  });

  it('can cancel setting up', async () => {
    const {user} = renderPage(off, {'POST /api/auth/me/two-factor/authenticator': () => json(authenticatorSetup)});

    await user.click(await screen.findByRole('button', {name: 'Set Up Two-Factor'}));
    await user.click(await screen.findByRole('button', {name: 'Cancel'}));

    expect(await screen.findByRole('button', {name: 'Set Up Two-Factor'})).toBeInTheDocument();
  });

  it('turns it off with the password and a code, and says the other sessions ended', async () => {
    withAnnouncements();
    const {user, fetch, queryClient} = renderPage(on, {[DISABLE]: () => new Response(null, {status: 204})});

    await turnOff(user, 'Synthetic-Passw0rd', 'abcd-1234');

    expect(await screen.findByText('Two-factor authentication is off. Your other sessions have ended.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
    expect(await bodyOf(fetch, 'POST', '/api/auth/me/two-factor/disable')).toEqual({password: 'Synthetic-Passw0rd', code: 'abcd-1234'});
    await waitFor(() => expect(calls(fetch).filter((c) => c === STATUS).length).toBe(2));
    expect(queryClient.getQueryState(twoFactorStatusQuery.queryKey)).toBeDefined();
    await expectAnnounced('Two-factor authentication turned off');
  });

  it('asks for the password with the username beside it, for password managers', async () => {
    const {user} = renderPage();

    await user.click(await screen.findByRole('button', {name: 'Turn Off'}));

    const password = within(openDialog()!).getByLabelText(/your password/i);
    expect(password).toHaveAttribute('autocomplete', 'current-password');
    expect(document.querySelector('input[name="username"]')).toHaveValue(adminUser.username);
    expect(isDisabled(within(openDialog()!).getByRole('button', {name: 'Turn Off'}))).toBe(true);
  });

  it("shows the API's reasons on the password and code, and anything else above them", async () => {
    const {user} = renderPage(on, {
      [DISABLE]: () =>
        json(
          {
            title: 'One or more validation errors occurred.',
            errors: {Password: ['Your password is incorrect.'], Code: ["That code isn't right."], '': ['Something else.']},
          },
          400,
        ),
    });

    const dialog = await turnOff(user);

    expect((await dialog.findAllByText('Your password is incorrect.')).length).toBeGreaterThan(0);
    expect(dialog.getAllByText("That code isn't right.").length).toBeGreaterThan(0);
    expect(dialog.getByText('Something else.')).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
  });

  it.each([
    [409, 'Admin accounts must use two-factor authentication here, so you can’t turn it off.'],
    [423, 'This account is locked. Try again in 5 minutes.'],
    [429, 'Too many codes for this account. Try again in 12 minutes.'],
  ])("says why a %s refused turning it off, in the API's words", async (status, title) => {
    const {user} = renderPage(on, {[DISABLE]: () => json({title, status}, status)});

    const dialog = await turnOff(user);

    expect(await dialog.findByText(title)).toBeInTheDocument();
  });

  it('says when it could not be turned off for another reason', async () => {
    const {user} = renderPage(on, {[DISABLE]: () => json({title: 'Server error'}, 500)});

    const dialog = await turnOff(user);

    expect(await dialog.findByText('Two-factor authentication could not be turned off. Please try again.')).toBeInTheDocument();
  });

  it('gets new recovery codes with the password and a code, shows them once, and forgets them on Done', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    const {user, fetch} = renderPage(on, {[CODES]: () => json({recoveryCodes})});

    const dialog = await getNewCodes(user);

    expect(await dialog.findByText(recoveryCodes[0])).toBeInTheDocument();
    expect(await bodyOf(fetch, 'POST', '/api/auth/me/two-factor/recovery-codes')).toEqual({password: 'Synthetic-Passw0rd', code: '123456'});
    await act(() => vi.advanceTimersByTimeAsync(1000));
    await user.click(dialog.getByRole('button', {name: 'Done'}));
    expect(openDialog()).toBeNull();
    await waitFor(() => expect(document.body.textContent).not.toContain(recoveryCodes[0]));
  });

  it('asks for new codes with the password, focused first, with the username beside it for password managers', async () => {
    const {user} = renderPage();

    await user.click(await screen.findByRole('button', {name: 'New Recovery Codes'}));
    const dialog = within(openDialog()!);

    const password = dialog.getByLabelText(/your password/i);
    expect(password).toHaveAttribute('autocomplete', 'current-password');
    expect(password).toHaveFocus();
    expect(document.querySelector('input[name="username"]')).toHaveValue(adminUser.username);
    expect(isDisabled(dialog.getByRole('button', {name: 'Get New Codes'}))).toBe(true);
    await user.type(dialog.getByLabelText(/code from your authenticator app/i), '123456');
    // A code alone isn't enough.
    expect(isDisabled(dialog.getByRole('button', {name: 'Get New Codes'}))).toBe(true);
  });

  it('gets them with Enter in the code, once both are given', async () => {
    const {user, fetch} = renderPage(on, {[CODES]: () => json({recoveryCodes})});

    await user.click(await screen.findByRole('button', {name: 'New Recovery Codes'}));
    const dialog = within(openDialog()!);
    await user.type(dialog.getByLabelText(/code from your authenticator app/i), '123456{Enter}');
    expect(calls(fetch)).not.toContain(CODES);
    await user.clear(dialog.getByLabelText(/code from your authenticator app/i));
    await user.type(dialog.getByLabelText(/your password/i), 'Synthetic-Passw0rd');
    await user.type(dialog.getByLabelText(/code from your authenticator app/i), '{Enter}');
    expect(calls(fetch)).not.toContain(CODES);
    await user.type(dialog.getByLabelText(/code from your authenticator app/i), '123456{Enter}');

    expect(await dialog.findByText(recoveryCodes[0])).toBeInTheDocument();
  });

  it("shows a wrong password and a wrong code on their fields, and anything else above them", async () => {
    const {user} = renderPage(on, {
      [CODES]: () =>
        json(
          {
            title: 'One or more validation errors occurred.',
            errors: {Password: ['Your password is incorrect.'], Code: ["That code isn't right."], '': ['Something else.']},
          },
          400,
        ),
    });

    const dialog = await getNewCodes(user, '000000');

    expect((await dialog.findAllByText("That code isn't right.")).length).toBeGreaterThan(0);
    expect(dialog.getAllByText('Your password is incorrect.').length).toBeGreaterThan(0);
    expect(dialog.getByLabelText(/your password/i)).toHaveAttribute('aria-invalid', 'true');
    expect(dialog.getByText('Something else.')).toBeInTheDocument();
  });

  it.each([
    [423, () => json({title: 'This account is locked. Try again in 5 minutes.', status: 423}, 423), 'This account is locked. Try again in 5 minutes.'],
    [429, () => json({title: 'Too many codes for this account. Try again in 12 minutes.', status: 429}, 429), 'Too many codes for this account. Try again in 12 minutes.'],
    [500, () => json({title: 'Server error'}, 500), 'New recovery codes could not be made. Please try again.'],
  ])('says when new codes could not be made (%s)', async (_, respond, message) => {
    const {user} = renderPage(on, {[CODES]: respond});

    const dialog = await getNewCodes(user);

    expect(await dialog.findByText(message)).toBeInTheDocument();
  });

  it('says when its settings could not be loaded', async () => {
    renderPage(undefined, {[STATUS]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('Your two-factor authentication settings could not be loaded. Please try again.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Set Up Two-Factor'})).not.toBeInTheDocument();
  });
});
