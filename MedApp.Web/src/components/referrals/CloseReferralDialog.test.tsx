import {screen, within} from '@testing-library/react';
import {isDisabled, openDialog, renderWithApp, staffUser} from '../../test/render';
import {testReferral} from '../../test/referrals';
import {CloseReferralDialog, type ReferralClosing} from './CloseReferralDialog';

function render(closing: ReferralClosing | null, props: Partial<Parameters<typeof CloseReferralDialog>[0]> = {}) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const result = renderWithApp(
    <CloseReferralDialog closing={closing} referral={testReferral()} isProcessing={false} onConfirm={onConfirm} onCancel={onCancel} {...props} />,
    {user: staffUser},
  );
  return {...result, onConfirm, onCancel};
}

describe('CloseReferralDialog', () => {
  it('shows the patient and the referral, and asks why it was withdrawn', async () => {
    const {user, onConfirm} = render('withdraw');

    expect(await screen.findByRole('heading', {name: 'Withdraw Referral?'})).toBeInTheDocument();
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('Dermatology')).toBeInTheDocument();
    expect(dialog.getByText('2 November 2026')).toBeInTheDocument();
    expect(dialog.getByText('Synthetic Street Surgery')).toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Withdraw Referral'}))).toBe(true);

    await user.type(dialog.getByRole('textbox', {name: /Who withdrew it/}), '  The practice phoned  ');
    await user.click(dialog.getByRole('button', {name: 'Withdraw Referral'}));
    expect(onConfirm).toHaveBeenCalledWith('The practice phoned');
  });

  it('marks it entered in error, showing the API\'s errors and messages', async () => {
    const {user, onCancel} = render('enteredInError', {
      errors: {title: 'Invalid', fields: {detail: ['Say what was wrong.']}, general: []},
      message: 'Someone changed this referral meanwhile.',
    });

    expect(await screen.findByRole('heading', {name: 'Mark Entered in Error?'})).toBeInTheDocument();
    const dialog = within(openDialog()!);
    expect(dialog.getByText('Say what was wrong.')).toBeInTheDocument();
    expect(dialog.getByText('Someone changed this referral meanwhile.')).toBeInTheDocument();
    expect(dialog.getByRole('textbox', {name: /What was wrong/})).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    expect(onCancel).toHaveBeenCalled();
  });

  it('is shut without a closing', async () => {
    render(null);
    await screen.findByTestId('announcer').catch(() => undefined);
    expect(openDialog()).toBeNull();
  });
});
