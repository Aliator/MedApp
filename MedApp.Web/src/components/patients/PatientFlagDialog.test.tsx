import {screen, waitFor, within} from '@testing-library/react';
import type {ValidationErrors} from '../../api/errors';
import {calls, isDisabled, json, mockApi, openDialog, pageOf, renderWithApp} from '../../test/render';
import {PatientFlagDialog, type PatientFlagChange} from './PatientFlagDialog';

const patientId = 'c184a7b8-2585-4437-bcd4-e1700be38299';
const SEARCH = '/api/procedures/patient-procedures/search';

function renderDialog(
  change: PatientFlagChange | null,
  {underWay = 0, errors = null, message = null}: {underWay?: number; errors?: ValidationErrors | null; message?: string | null} = {},
) {
  const fetch = mockApi({[`GET ${SEARCH}`]: () => json(pageOf([], underWay))});
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const result = renderWithApp(
    <PatientFlagDialog
      patientId={patientId}
      change={change}
      isProcessing={false}
      errors={errors}
      message={message}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />,
  );
  return {...result, fetch, onConfirm, onCancel};
}

describe('PatientFlagDialog', () => {
  it('needs a reason to restrict a record, and passes it on trimmed', async () => {
    const {user, onConfirm, fetch} = renderDialog({flag: 'restriction', set: true});

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/nobody can change this patient/)).toBeInTheDocument();
    const confirm = within(dialog).getByRole('button', {name: 'Restrict Record'});
    expect(isDisabled(confirm)).toBe(true);

    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), '  Synthetic accuracy check ');
    await waitFor(() => expect(isDisabled(confirm)).toBe(false));
    await user.click(confirm);

    expect(onConfirm).toHaveBeenCalledWith('Synthetic accuracy check', false);
    // It asked how many procedures are under way: the open ones only, one row for the total.
    expect(calls(fetch)).toEqual([`GET ${SEARCH}?PatientId=${patientId}&PageSize=1`]);
  });

  it('says how many procedures are under way, and restricts only once the admin confirms their work stops', async () => {
    const {user, onConfirm} = renderDialog({flag: 'restriction', set: true}, {underWay: 2});

    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText(/2 procedures are under way/)).toHaveTextContent(/can still be blocked or cancelled, with a reason/);
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Synthetic accuracy check');
    const confirm = within(dialog).getByRole('button', {name: 'Restrict Record'});
    expect(isDisabled(confirm)).toBe(true);

    await user.click(within(dialog).getByRole('checkbox', {name: /work on these 2 procedures stops/}));
    expect(isDisabled(confirm)).toBe(false);
    await user.click(confirm);

    expect(onConfirm).toHaveBeenCalledWith('Synthetic accuracy check', true);
  });

  it('names one procedure under way in the singular', async () => {
    renderDialog({flag: 'restriction', set: true}, {underWay: 1});

    expect(await screen.findByText(/1 procedure is under way\. While the record is restricted, work on it stops/)).toBeInTheDocument();
    expect(screen.getByRole('checkbox', {name: /work on this procedure stops/})).toBeInTheDocument();
  });

  it('shows why the last try failed, on the dialog and its field', async () => {
    const errors: ValidationErrors = {title: 'One or more validation errors occurred.', fields: {reason: ['Give a reason.']}, general: []};
    renderDialog({flag: 'dispute', set: true}, {errors, message: 'Someone else changed this record.'});

    expect(await screen.findByText('Someone else changed this record.')).toBeInTheDocument();
    expect(screen.getByText('Give a reason.')).toBeInTheDocument();
  });

  it('lifts a restriction without needing a reason, or asking about procedures', async () => {
    const {user, onConfirm, fetch} = renderDialog({flag: 'restriction', set: false});

    await screen.findByRole('dialog');
    expect(within(openDialog()!).getByRole('textbox', {name: /reason \(optional\)/i})).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Lift Restriction'}));

    expect(onConfirm).toHaveBeenCalledWith('', false);
    expect(calls(fetch)).toEqual([]);
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
