import {screen, waitFor} from '@testing-library/react';
import {StrictMode} from 'react';
import {twoFactorStatusQuery} from '../../api/account';
import {adminUser, calls, expectAnnounced, findAlert, json, mockApi, renderWithApp, withAnnouncements} from '../../test/render';
import {authenticatorSetup, recoveryCodes} from '../../test/twoFactor';
import {TwoFactorSetup} from './TwoFactorSetup';

const AUTHENTICATOR = 'POST /api/auth/me/two-factor/authenticator';
const ENABLE = 'POST /api/auth/me/two-factor/enable';

function renderSetup(handlers: Parameters<typeof mockApi>[0] = {}, props: Partial<Parameters<typeof TwoFactorSetup>[0]> = {}) {
  const fetch = mockApi({
    [AUTHENTICATOR]: () => json(authenticatorSetup),
    [ENABLE]: () => json({recoveryCodes}),
    ...handlers,
  });
  const onDone = vi.fn();
  const onEnabled = vi.fn();
  const result = renderWithApp(
    <StrictMode>
      <TwoFactorSetup username="test-admin" doneLabel="Continue" onDone={onDone} onEnabled={onEnabled} {...props} />
    </StrictMode>,
    {user: adminUser},
  );
  return {...result, fetch, onDone, onEnabled};
}

type User = ReturnType<typeof renderSetup>['user'];

async function turnOn(user: User, code = '123456') {
  await user.type(await screen.findByLabelText(/code from your authenticator app/i), code);
  await user.click(screen.getByRole('button', {name: 'Turn on two-factor'}));
}

describe('TwoFactorSetup', () => {
  it('asks for one new key as it opens, and shows it as a QR code on white, whatever the theme', async () => {
    const {fetch} = renderSetup();

    const qr = await screen.findByRole('img', {name: 'QR code for your authenticator app'});
    expect(qr).toHaveAttribute('viewBox', '0 0 29 29');
    expect(qr.querySelector('rect')).toHaveAttribute('fill', '#ffffff');
    expect(qr.querySelector('path')).toHaveAttribute('d', authenticatorSetup.qrCodePath);
    expect(qr.querySelector('path')).toHaveAttribute('fill', '#000000');
    // Once, though StrictMode runs effects twice.
    expect(calls(fetch)).toEqual([AUTHENTICATOR]);
  });

  it('shows the key as text, and copies it without its spaces', async () => {
    const {user} = renderSetup();

    expect(await screen.findByText('abcd efgh ijkl mnop')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Copy key'}));

    expect(await screen.findByRole('button', {name: 'Copied'})).toBeInTheDocument();
    expect(await navigator.clipboard.readText()).toBe('abcdefghijklmnop');
  });

  it('asks for the code in a one-time-code field on a number pad', async () => {
    renderSetup();

    const code = await screen.findByLabelText(/code from your authenticator app/i);
    expect(code).toHaveAttribute('autocomplete', 'one-time-code');
    expect(code).toHaveAttribute('inputmode', 'numeric');
  });

  it('turns two-factor on with the code, then shows the recovery codes once, until Continue', async () => {
    withAnnouncements();
    const {user, fetch, onDone, onEnabled, queryClient} = renderSetup();
    queryClient.setQueryData(twoFactorStatusQuery.queryKey, {enabled: false, recoveryCodesLeft: 0, required: true});

    await turnOn(user);

    expect(await screen.findByText(recoveryCodes[0])).toBeInTheDocument();
    expect(screen.queryByRole('img', {name: /QR code/})).not.toBeInTheDocument();
    expect(await (fetch.mock.calls[1][0] as Request).json()).toEqual({code: '123456'});
    expect(onEnabled).toHaveBeenCalledOnce();
    expect(queryClient.getQueryState(twoFactorStatusQuery.queryKey)?.isInvalidated).toBe(true);
    await expectAnnounced(/Two-factor authentication is on\./);
    expect(onDone).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', {name: 'Continue'}));
    expect(onDone).toHaveBeenCalledOnce();
  });

  it('shows a wrong code on its field, and stays', async () => {
    const {user} = renderSetup({
      [ENABLE]: () => json({title: 'One or more validation errors occurred.', errors: {Code: ["That code isn't right."]}}, 400),
    });

    await turnOn(user, '000000');

    expect(await screen.findByText("That code isn't right.")).toBeInTheDocument();
    expect(screen.getByLabelText(/code from your authenticator app/i)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('img', {name: /QR code/})).toBeInTheDocument();
  });

  it.each([
    ['already on', () => json({title: 'Two-factor authentication is already on for this account.', status: 409}, 409), 'Two-factor authentication is already on for this account.'],
    ['a server error', () => json({title: 'Server error'}, 500), 'Two-factor authentication could not be turned on. Please try again.'],
  ])('says when it could not be turned on: %s', async (_, respond, message) => {
    const {user} = renderSetup({[ENABLE]: respond});

    await turnOn(user);

    expect(await findAlert(message)).toBeInTheDocument();
  });

  it("says when a key couldn't be made, in the API's words for a 409, and tries again", async () => {
    let attempts = 0;
    const {user} = renderSetup({
      [AUTHENTICATOR]: () => (++attempts === 1 ? json({title: 'Two-factor authentication is already on.', status: 409}, 409) : json(authenticatorSetup)),
    });

    expect(await findAlert('Two-factor authentication is already on.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Try again'}));

    expect(await screen.findByRole('img', {name: /QR code/})).toBeInTheDocument();
  });

  it("says a key couldn't be made for another reason, and offers Cancel when there's somewhere to go", async () => {
    const onCancel = vi.fn();
    const {user} = renderSetup({[AUTHENTICATOR]: () => json({title: 'Server error'}, 500)}, {onCancel});

    expect(await findAlert('A key for your authenticator app could not be made. Please try again.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Cancel'}));

    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('offers Cancel while setting up, and none without somewhere to go', async () => {
    const onCancel = vi.fn();
    const {user} = renderSetup({}, {onCancel});

    await user.click(await screen.findByRole('button', {name: 'Cancel'}));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('shows no Cancel when there is nowhere to go back to', async () => {
    renderSetup();

    await screen.findByRole('img', {name: /QR code/});
    await waitFor(() => expect(screen.queryByRole('button', {name: 'Cancel'})).not.toBeInTheDocument());
  });
});
