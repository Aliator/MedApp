import {screen, within} from '@testing-library/react';
import {isDisabled, openDialog, renderWithApp} from '../../test/render';
import {PatientFlagDialog, type PatientFlagChange} from './PatientFlagDialog';

function renderDialog(change: PatientFlagChange | null, errors = null) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const result = renderWithApp(<PatientFlagDialog change={change} isProcessing={false} errors={errors} onConfirm={onConfirm} onCancel={onCancel} />);
  return {...result, onConfirm, onCancel};
}

describe('PatientFlagDialog', () => {
  it('needs a reason to restrict a record, and passes it on trimmed', async () => {
    const {user, onConfirm} = renderDialog({flag: 'restriction', set: true});

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/nobody can change this patient/)).toBeInTheDocument();
    const confirm = within(dialog).getByRole('button', {name: 'Restrict Record'});
    expect(isDisabled(confirm)).toBe(true);

    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), '  Synthetic accuracy check ');
    await user.click(confirm);

    expect(onConfirm).toHaveBeenCalledWith('Synthetic accuracy check');
  });

  it('lifts a restriction without needing a reason', async () => {
    const {user, onConfirm} = renderDialog({flag: 'restriction', set: false});

    await screen.findByRole('dialog');
    expect(within(openDialog()!).getByRole('textbox', {name: /reason \(optional\)/i})).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Lift Restriction'}));

    expect(onConfirm).toHaveBeenCalledWith('');
  });

  it('asks what is disputed, and clears a dispute', async () => {
    renderDialog({flag: 'dispute', set: true});
    expect(await screen.findByRole('textbox', {name: /what's disputed/i})).toBeInTheDocument();
    expect(isDisabled(within(openDialog()!).getByRole('button', {name: 'Mark Disputed'}))).toBe(true);
  });

  it('clears a dispute, and can be cancelled', async () => {
    const {user, onCancel} = renderDialog({flag: 'dispute', set: false});

    await screen.findByRole('dialog');
    expect(within(openDialog()!).getByRole('button', {name: 'Clear Dispute'})).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(onCancel).toHaveBeenCalled();
  });

  it('is closed with no change', () => {
    renderDialog(null);

    expect(openDialog()).toBeNull();
  });
});
