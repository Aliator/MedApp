import {screen, within} from '@testing-library/react';
import {isDisabled, openDialog, renderWithApp} from '../../test/render';
import {StatusChangeDialog, statusAction} from './StatusChangeDialog';

function renderDialog(to: string) {
  const onConfirm = vi.fn();
  const result = renderWithApp(<StatusChangeDialog to={to} isProcessing={false} onConfirm={onConfirm} onCancel={() => {}} />);
  return {...result, onConfirm};
}

describe('StatusChangeDialog', () => {
  it('names each change by what it does', () => {
    expect(statusAction('Blocked').label).toBe('Put on Hold');
    expect(statusAction('Cancelled').label).toBe('Cancel Pathway');
    expect(statusAction('Completed').label).toBe('Mark Completed');
    expect(statusAction('Archived').label).toBe('Archive');
    expect(statusAction('InProgress')).toMatchObject({label: 'Take off Hold', message: expect.stringContaining('in progress')});
  });

  it('needs a reason to block, and passes it on', async () => {
    const {user, onConfirm} = renderDialog('Blocked');

    const dialog = await screen.findByRole('dialog');
    const confirm = within(dialog).getByRole('button', {name: 'Put on Hold'});
    expect(isDisabled(confirm)).toBe(true);

    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), '  Waiting for bloods. ');
    await user.click(confirm);

    expect(onConfirm).toHaveBeenCalledWith('Waiting for bloods.');
  });

  it('completes without asking for a reason', async () => {
    const {user, onConfirm} = renderDialog('Completed');

    await screen.findByRole('dialog');
    expect(within(openDialog()!).queryByRole('textbox')).not.toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Mark Completed'}));

    expect(onConfirm).toHaveBeenCalledWith(undefined);
  });
});
