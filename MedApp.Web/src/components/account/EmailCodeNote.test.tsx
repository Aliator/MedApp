import {act, screen} from '@testing-library/react';
import {isDisabled, renderWithApp} from '../../test/render';
import {emailCodeSent} from '../../test/twoFactor';
import {EmailCodeNote} from './EmailCodeNote';

const EXHAUSTED = 'No more codes for now.';

function renderNote(sent = emailCodeSent) {
  const onResend = vi.fn();
  const result = renderWithApp(<EmailCodeNote sent={sent} onResend={onResend} exhaustedMessage={EXHAUSTED} />);
  return {...result, onResend};
}

describe('EmailCodeNote', () => {
  it('says where the code went, as the server masked the address', async () => {
    renderNote();

    expect(await screen.findByText(/We emailed a code to/)).toBeInTheDocument();
    expect(screen.getByText(`We emailed a code to ${emailCodeSent.sentTo}.`, {exact: false})).toBeInTheDocument();
  });

  it('counts down until another code can be sent, then sends one', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    const {user, onResend} = renderNote({...emailCodeSent, resendInSeconds: 3});

    expect(isDisabled(await screen.findByRole('button', {name: 'Resend in 3 s'}))).toBe(true);
    await act(() => vi.advanceTimersByTimeAsync(1000));
    expect(screen.getByRole('button', {name: 'Resend in 2 s'})).toBeInTheDocument();
    await act(() => vi.advanceTimersByTimeAsync(2000));
    await user.click(screen.getByRole('button', {name: 'Resend code'}));

    expect(onResend).toHaveBeenCalledOnce();
  });

  it('offers Resend at once when the server allows it', async () => {
    renderNote({...emailCodeSent, resendInSeconds: 0});

    expect(isDisabled(await screen.findByRole('button', {name: 'Resend code'}))).toBe(false);
  });

  it('says so, instead of Resend, when no more codes can be sent', async () => {
    renderNote({...emailCodeSent, resendsLeft: 0});

    expect(await screen.findByText(EXHAUSTED)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /resend/i})).not.toBeInTheDocument();
  });
});
