import {screen} from '@testing-library/react';
import {useSendTwoFactorEmailCode} from '../../api/account';
import {calls, expectAnnounced, isDisabled, json, mockApi, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {emailCodeSent} from '../../test/twoFactor';
import {CHANGE_NO_MORE_CODES, EmailMeACode, PASSWORD_FIRST} from './EmailMeACode';

const SEND_CODE = 'POST /api/auth/me/two-factor/email/code';

function Harness({password}: {password: string}) {
  const send = useSendTwoFactorEmailCode();
  return <EmailMeACode send={send} password={password} />;
}

function renderButton(password: string, handler: Parameters<typeof mockApi>[0][string] = () => json(emailCodeSent, 202)) {
  const fetch = mockApi({[SEND_CODE]: handler});
  return {...renderWithApp(<Harness password={password} />, {user: staffUser}), fetch};
}

const sentBodies = (fetch: ReturnType<typeof mockApi>) =>
  Promise.all(
    fetch.mock.calls
      .map(([r]) => r as Request)
      .filter((r) => new URL(r.url).pathname === '/api/auth/me/two-factor/email/code')
      .map((r) => r.clone().json()),
  );

describe('EmailMeACode', () => {
  it('waits for the password before it can send anything', async () => {
    const {fetch} = renderButton('');

    expect(isDisabled(await screen.findByRole('button', {name: 'Email me a code'}))).toBe(true);
    expect(screen.getByText(PASSWORD_FIRST)).toBeInTheDocument();
    expect(calls(fetch)).not.toContain(SEND_CODE);
  });

  it('sends the password with the request, then says where the code went', async () => {
    const {user, fetch} = renderButton('Synthetic-Passw0rd');

    expect(screen.queryByText(PASSWORD_FIRST)).not.toBeInTheDocument();
    await user.click(await screen.findByRole('button', {name: 'Email me a code'}));

    expect(await screen.findByText(`We emailed a code to ${emailCodeSent.sentTo}.`, {exact: false})).toBeInTheDocument();
    expect(await sentBodies(fetch)).toEqual([{password: 'Synthetic-Passw0rd'}]);
  });

  it('sends again with the password, telling screen readers', async () => {
    withAnnouncements();
    const {user, fetch} = renderButton('Synthetic-Passw0rd', () => json({...emailCodeSent, resendInSeconds: 0}, 202));

    await user.click(await screen.findByRole('button', {name: 'Email me a code'}));
    await user.click(await screen.findByRole('button', {name: 'Resend code'}));

    await expectAnnounced(`A new code was emailed to ${emailCodeSent.sentTo}.`);
    expect(await sentBodies(fetch)).toEqual([{password: 'Synthetic-Passw0rd'}, {password: 'Synthetic-Passw0rd'}]);
  });

  it('says when no more codes can be sent', async () => {
    const {user} = renderButton('Synthetic-Passw0rd', () => json({...emailCodeSent, resendsLeft: 0}, 202));

    await user.click(await screen.findByRole('button', {name: 'Email me a code'}));

    expect(await screen.findByText(CHANGE_NO_MORE_CODES)).toBeInTheDocument();
  });

  it("shows a refusal in the API's words", async () => {
    const unavailable = "The code couldn't be sent just now. Try again later, or use a recovery code.";
    const {user} = renderButton('Synthetic-Passw0rd', () => json({title: unavailable, status: 409}, 409));

    await user.click(await screen.findByRole('button', {name: 'Email me a code'}));

    expect(await screen.findByText(unavailable)).toBeInTheDocument();
  });

  it("shows a validation message that isn't about the password", async () => {
    const {user} = renderButton('Synthetic-Passw0rd', () =>
      json({title: 'One or more validation errors occurred.', status: 400, errors: {'': ['Something else was wrong.']}}, 400),
    );

    await user.click(await screen.findByRole('button', {name: 'Email me a code'}));

    expect(await screen.findByText('Something else was wrong.')).toBeInTheDocument();
  });
});
