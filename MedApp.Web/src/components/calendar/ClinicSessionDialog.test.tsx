import {screen, waitFor, within} from '@testing-library/react';
import {testSession, testSessionDetail} from '../../test/clinics';
import {expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {cancelledWords, ClinicSessionDialog, type ClinicSessionDialogProps} from './ClinicSessionDialog';

const SESSION = testSession({booked: 1});

function renderDialog(props: Partial<ClinicSessionDialogProps> = {}, handlers: Parameters<typeof mockApi>[0] = {}, detail = testSessionDetail(SESSION)) {
  const onClose = vi.fn();
  const fetch = mockApi({'GET /api/clinics/sessions/session-1': () => json(detail), ...handlers});
  const result = renderWithApp(<ClinicSessionDialog sessionId="session-1" onClose={onClose} {...props} />, {user: staffUser});
  return {...result, fetch, onClose};
}

const dialog = () => within(openDialog() ?? document.body);

describe('ClinicSessionDialog', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T10:00:00Z')});
  });

  it('shows the session and each slot, free or with its patient, and books a free one', async () => {
    const onBookSlot = vi.fn();
    const {user} = renderDialog({onBookSlot});

    expect(await screen.findByRole('heading', {name: 'Clinic Session'})).toBeInTheDocument();
    expect(await dialog().findByText('Tuesday 24 November, 09:00 to 12:00')).toBeInTheDocument();
    expect(dialog().getByText('Room 2, Main site')).toBeInTheDocument();
    expect(dialog().getByText('Synthetic dermatology')).toBeInTheDocument();
    expect(dialog().getByText('20 minutes · 8 of 9 free')).toBeInTheDocument();
    const slots = dialog().getByRole('list', {name: 'Slots'});
    expect(within(slots).getAllByRole('listitem')).toHaveLength(9);
    expect(within(slots).getByText('Test Patient0')).toBeInTheDocument();

    await user.click(dialog().getByRole('button', {name: 'Book 09:20'}));
    expect(onBookSlot).toHaveBeenCalledWith(expect.objectContaining({id: 'session-1'}), '2026-11-24T09:20:00.000Z');
  });

  it('cancels with a reason, then lists the patients to tell', async () => {
    withAnnouncements();
    const cancelled = {
      session: {...SESSION, status: 'Cancelled', cancelReason: 'Clinician away', freeSlotCount: 0, freeSlotStartsUtc: []},
      patientsToTell: [{appointmentId: 'appointment-0', patientId: 'patient-0', patientName: 'Test Patient', nhsNumber: '9990000018', startUtc: SESSION.startUtc}],
    };
    const {user, fetch} = renderDialog({}, {'POST /api/clinics/sessions/session-1/cancel': () => json(cancelled)});

    await user.click(await dialog().findByRole('button', {name: 'Cancel Session'}));
    expect(dialog().getByText(/Cancelling it cancels the appointment booked into it that haven’t started.*Any that have started stay/)).toBeInTheDocument();
    const confirm = dialog().getAllByRole('button', {name: 'Cancel Session'}).at(-1)!;
    expect(isDisabled(confirm)).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /Reason/}), 'Clinician away');
    await user.click(dialog().getAllByRole('button', {name: 'Cancel Session'}).at(-1)!);

    await expectAnnounced('Clinic session cancelled');
    expect(dialog().getByText('Clinic session cancelled, with its appointment. Please tell this patient.')).toBeInTheDocument();
    const toTell = dialog().getByRole('list', {name: 'Patients to tell'});
    expect(within(toTell).getByText('Test Patient')).toBeInTheDocument();
    expect(within(toTell).getByText('NHS No. 999 000 0018')).toBeInTheDocument();
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await request.clone().json()).toEqual({reason: 'Clinician away', expectedLastUpdated: SESSION.lastUpdated});
  });

  it('offers to tell the patients while messages are on, ticked, and says messages will go', async () => {
    const cancelled = {
      session: {...SESSION, status: 'Cancelled', cancelReason: 'Clinician away'},
      patientsToTell: [{appointmentId: 'appointment-0', patientId: 'patient-0', patientName: 'Test Patient', nhsNumber: '9990000018', startUtc: SESSION.startUtc}],
    };
    const {user, fetch} = renderDialog(
      {},
      {
        'GET /api/appointments/patient-messages/status': () => json({mode: 'Offline', isOn: true, sendsMissedAppointmentMessages: false, reminderDaysBefore: 2}),
        'POST /api/clinics/sessions/session-1/cancel': () => json(cancelled),
      },
    );

    await user.click(await dialog().findByRole('button', {name: 'Cancel Session'}));
    const tell = await dialog().findByRole('checkbox', {name: 'Tell the patients by text or email'});
    expect(tell).toBeChecked();
    await user.type(dialog().getByRole('textbox', {name: /Reason/}), 'Clinician away');
    await user.click(dialog().getAllByRole('button', {name: 'Cancel Session'}).at(-1)!);

    expect(await dialog().findByText(/A text or email will go to the patient if they chose one; please tell anyone who didn't\./)).toBeInTheDocument();
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await request.clone().json()).toEqual({reason: 'Clinician away', expectedLastUpdated: SESSION.lastUpdated, tellPatients: true});
  });

  it('sends tellPatients false when it is unticked, and asks staff to tell them', async () => {
    const cancelled = {session: {...SESSION, status: 'Cancelled'}, patientsToTell: [{appointmentId: 'a', patientName: 'Test Patient', startUtc: SESSION.startUtc}]};
    const {user, fetch} = renderDialog(
      {},
      {
        'GET /api/appointments/patient-messages/status': () => json({mode: 'Live', isOn: true}),
        'POST /api/clinics/sessions/session-1/cancel': () => json(cancelled),
      },
    );

    await user.click(await dialog().findByRole('button', {name: 'Cancel Session'}));
    await user.click(await dialog().findByRole('checkbox', {name: 'Tell the patients by text or email'}));
    await user.type(dialog().getByRole('textbox', {name: /Reason/}), 'Room closed');
    await user.click(dialog().getAllByRole('button', {name: 'Cancel Session'}).at(-1)!);

    expect(await dialog().findByText('Clinic session cancelled, with its appointment. Please tell this patient.')).toBeInTheDocument();
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await request.clone().json()).toMatchObject({tellPatients: false});
  });

  it("shows the API's words when cancelling is refused", async () => {
    const {user} = renderDialog({}, {'POST /api/clinics/sessions/session-1/cancel': () => json({title: 'Someone changed this clinic session.'}, 409)});

    await user.click(await dialog().findByRole('button', {name: 'Cancel Session'}));
    await user.type(dialog().getByRole('textbox', {name: /Reason/}), 'Away');
    await user.click(dialog().getAllByRole('button', {name: 'Cancel Session'}).at(-1)!);

    expect(await dialog().findByText('Someone changed this clinic session.')).toBeInTheDocument();
  });

  it('offers Edit and Cancel only when it can be changed, and Book only with onBookSlot', async () => {
    const onEdit = vi.fn();
    const {user} = renderDialog({onEdit});
    await user.click(await dialog().findByRole('button', {name: 'Edit'}));
    expect(onEdit).toHaveBeenCalledWith(expect.objectContaining({id: 'session-1'}));
    expect(dialog().queryByRole('button', {name: /^Book/})).not.toBeInTheDocument();
  });

  it('a session that cannot be changed has neither', async () => {
    renderDialog({onEdit: vi.fn()}, {}, testSessionDetail({...SESSION, canChange: false}));
    await dialog().findByText('Room 2, Main site');
    expect(dialog().queryByRole('button', {name: 'Edit'})).not.toBeInTheDocument();
    expect(dialog().queryByRole('button', {name: 'Cancel Session'})).not.toBeInTheDocument();
  });

  it('says when it could not be loaded', async () => {
    renderDialog({}, {'GET /api/clinics/sessions/session-1': () => json({title: 'Not found'}, 404)});
    await waitFor(() => expect(dialog().getByText(/could not be loaded/)).toBeInTheDocument());
  });

  it('shows a session cancelled earlier with its list of patients to tell, and nothing to change', async () => {
    const detail = {
      ...testSessionDetail({...SESSION, status: 'Cancelled', canChange: false, cancelReason: 'Clinician away'}),
      patientsToTell: [{appointmentId: 'appointment-1', patientId: 'patient-1', patientName: 'Told Patient', nhsNumber: null, startUtc: SESSION.startUtc}],
    };
    renderDialog({}, {}, detail);

    const toTell = await dialog().findByRole('list', {name: 'Patients to tell'});
    expect(within(toTell).getByText('Told Patient')).toBeInTheDocument();
    expect(within(toTell).getByText('No NHS number')).toBeInTheDocument();
    expect(dialog().getByText('Clinician away')).toBeInTheDocument();
    expect(dialog().queryByRole('button', {name: 'Cancel Session'})).not.toBeInTheDocument();
  });

  it('describes a cancellation with nobody booked', () => {
    expect(cancelledWords({session: SESSION, patientsToTell: []})).toBe('Clinic session cancelled. Nobody was booked into it.');
    expect(cancelledWords({session: SESSION, patientsToTell: [{}, {}]})).toBe('Clinic session cancelled, with its 2 appointments. Please tell these patients.');
    expect(cancelledWords({session: SESSION, patientsToTell: [{}, {}]}, true)).toBe(
      "Clinic session cancelled, with its 2 appointments. A text or email will go to each patient who chose one; please tell anyone who didn't.",
    );
    expect(cancelledWords({session: SESSION, patientsToTell: []}, true)).toBe('Clinic session cancelled. Nobody was booked into it.');
  });

  it('closes, goes back from cancelling, and words outcomes already recorded', async () => {
    const detail = testSessionDetail(SESSION);
    detail.slots![0] = {...detail.slots![0], appointmentStatus: 'DidNotAttend'};
    const {user, onClose} = renderDialog(
      {},
      {'POST /api/clinics/sessions/session-1/cancel': () => json({session: {...SESSION, status: 'Cancelled'}, patientsToTell: []})},
      detail,
    );

    expect(await dialog().findByText('Test Patient0 (did not attend)')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel Session'}));
    await user.click(dialog().getByRole('button', {name: 'Back'}));
    expect(dialog().getByRole('list', {name: 'Slots'})).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel Session'}));
    await user.type(dialog().getByRole('textbox', {name: /Reason/}), 'Room closed');
    await user.click(dialog().getAllByRole('button', {name: 'Cancel Session'}).at(-1)!);
    expect(await dialog().findByText('Clinic session cancelled. Nobody was booked into it.')).toBeInTheDocument();
    expect(dialog().queryByRole('list', {name: 'Patients to tell'})).not.toBeInTheDocument();
    await user.click(dialog().getAllByRole('button', {name: 'Close'}).at(-1)!);
    expect(onClose).toHaveBeenCalled();
  });
});
