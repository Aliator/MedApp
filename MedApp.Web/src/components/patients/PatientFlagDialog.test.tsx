import {screen, waitFor, within} from '@testing-library/react';
import type {ValidationErrors} from '../../api/errors';
import {calls, isDisabled, json, mockApi, openDialog, pageOf, renderWithApp} from '../../test/render';
import {PatientFlagDialog, type PatientFlagChange} from './PatientFlagDialog';

const patientId = 'c184a7b8-2585-4437-bcd4-e1700be38299';
const SEARCH = '/api/pathways/patient-pathways/search';
const APPOINTMENTS = '/api/appointments/search';

function renderDialog(
  change: PatientFlagChange | null,
  {
    underWay = 0,
    openAppointments = 0,
    errors = null,
    message = null,
  }: {underWay?: number; openAppointments?: number; errors?: ValidationErrors | null; message?: string | null} = {},
) {
  const fetch = mockApi({
    [`GET ${SEARCH}`]: () => json(pageOf([], underWay)),
    [`GET ${APPOINTMENTS}`]: () => json(pageOf([], openAppointments)),
  });
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
    // It asked how many pathways are under way and how many appointments are open: one row of each, for the totals.
    expect(calls(fetch)).toEqual([
      `GET ${SEARCH}?PatientId=${patientId}&PageSize=1`,
      `GET ${APPOINTMENTS}?PatientId=${patientId}&Statuses=Requested&Statuses=Booked&PageSize=1`,
    ]);
  });

  it('says how many pathways are under way, and restricts only once the admin confirms their work stops', async () => {
    const {user, onConfirm} = renderDialog({flag: 'restriction', set: true}, {underWay: 2});

    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText(/2 pathways are under way/)).toHaveTextContent(/can still be blocked or cancelled, with a reason/);
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Synthetic accuracy check');
    const confirm = within(dialog).getByRole('button', {name: 'Restrict Record'});
    expect(isDisabled(confirm)).toBe(true);

    await user.click(within(dialog).getByRole('checkbox', {name: /work on these 2 pathways stops/}));
    expect(isDisabled(confirm)).toBe(false);
    await user.click(confirm);

    expect(onConfirm).toHaveBeenCalledWith('Synthetic accuracy check', true);
  });

  it('names one pathway under way in the singular', async () => {
    renderDialog({flag: 'restriction', set: true}, {underWay: 1});

    expect(await screen.findByText(/1 pathway is under way\. While the record is restricted, work on it stops/)).toBeInTheDocument();
    expect(screen.getByRole('checkbox', {name: /work on this pathway stops/})).toBeInTheDocument();
  });

  it('says how many appointments are open with nothing under way, and restricts only once the admin confirms', async () => {
    const {user, onConfirm} = renderDialog({flag: 'restriction', set: true}, {openAppointments: 2});

    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText(/2 appointments are open/)).toHaveTextContent(
      "2 appointments are open. While the record is restricted, they can't be booked, moved or given an outcome: they can still be cancelled, but nothing else.",
    );
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Synthetic accuracy check');
    const confirm = within(dialog).getByRole('button', {name: 'Restrict Record'});
    expect(isDisabled(confirm)).toBe(true);

    await user.click(within(dialog).getByRole('checkbox', {name: /work on these 2 appointments stops/}));
    await user.click(confirm);

    expect(onConfirm).toHaveBeenCalledWith('Synthetic accuracy check', true);
  });

  it('names one open appointment in the singular', async () => {
    renderDialog({flag: 'restriction', set: true}, {openAppointments: 1});

    expect(await screen.findByText(/1 appointment is open\. While the record is restricted, it can't be booked/)).toBeInTheDocument();
    expect(screen.getByRole('checkbox', {name: /work on this appointment stops/})).toBeInTheDocument();
  });

  it('counts pathways under way and open appointments together', async () => {
    renderDialog({flag: 'restriction', set: true}, {underWay: 1, openAppointments: 3});

    expect(await screen.findByText(/1 pathway is under way and 3 appointments are open/)).toHaveTextContent(
      'the pathways can still be blocked or cancelled, with a reason, and the appointments cancelled, but nothing else.',
    );
    expect(screen.getByRole('checkbox', {name: /work on the 1 pathway and 3 appointments stops/})).toBeInTheDocument();
  });

  it("asks for the confirmation when the API does, though its own counts found nothing, and shows the API's message", async () => {
    const apiMessage = 'This patient has 1 open appointment. Confirm that it can’t be booked, moved or given an outcome.';
    const errors: ValidationErrors = {title: 'One or more validation errors occurred.', fields: {confirmOpenPathways: [apiMessage]}, general: []};
    const {user, onConfirm} = renderDialog({flag: 'restriction', set: true}, {errors});

    const checkbox = await screen.findByRole('checkbox', {name: /work on this patient's pathways and appointments stops/});
    expect(screen.getByText(apiMessage)).toBeInTheDocument();
    await user.type(screen.getByRole('textbox', {name: /reason/i}), 'Synthetic accuracy check');
    await user.click(checkbox);
    await user.click(screen.getByRole('button', {name: 'Restrict Record'}));

    expect(onConfirm).toHaveBeenCalledWith('Synthetic accuracy check', true);
  });

  it('shows why the last try failed, on the dialog and its field', async () => {
    const errors: ValidationErrors = {title: 'One or more validation errors occurred.', fields: {reason: ['Give a reason.']}, general: []};
    renderDialog({flag: 'dispute', set: true}, {errors, message: 'Someone else changed this record.'});

    expect(await screen.findByText('Someone else changed this record.')).toBeInTheDocument();
    expect(screen.getByText('Give a reason.')).toBeInTheDocument();
  });

  it('lifts a restriction without needing a reason, or asking about pathways', async () => {
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
