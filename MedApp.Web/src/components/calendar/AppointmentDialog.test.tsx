import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import type {Appointment} from '../../api/appointments';
import {STAFF_ME, TEST_PATIENT, testAppointment, testCalendar, testRequested} from '../../test/calendar';
import {adminUser, calls, expectAnnounced, isDisabled, json, managerUser, mockApi, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {AppointmentDialog, appointmentWhen} from './AppointmentDialog';

const MONDAY = '2026-11-23';
/** Patient messages switched on (to the development mailbox). */
const MESSAGES_ON = {mode: 'Offline', isOn: true, sendsMissedAppointmentMessages: true, reminderDaysBefore: 2};

function renderDialog(appointment: Appointment, handlers: Parameters<typeof mockApi>[0] = {}, stepMinutes?: number, user = staffUser) {
  const onClose = vi.fn();
  const fetch = mockApi({
    [`GET /api/appointments/${appointment.id}`]: () => json(appointment),
    'GET /api/calendar/staff': () => json([STAFF_ME]),
    'GET /api/calendar': (request) => json(testCalendar({from: new URL(request.url).searchParams.get('From')!, dayCount: 5, today: MONDAY})),
    'GET /api/patients/patient-1': () => json(TEST_PATIENT),
    ...handlers,
  });
  const result = renderWithApp(
    <SaveShortcutProvider>
      <AppointmentDialog appointment={appointment} onClose={onClose} stepMinutes={stepMinutes} />
    </SaveShortcutProvider>,
    {user},
  );
  return {...result, fetch, onClose};
}

/** The open dialog, or the whole page until one opens (for `findBy` queries, which wait). */
const dialog = () => within(openDialog() ?? document.body);
const bodyOf = async (fetch: ReturnType<typeof mockApi>, method: string, path: string) => {
  const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method && new URL(r.url).pathname === path)!;
  return request.clone().json();
};

// Dialogs that open further dialogs are slow under coverage on a loaded machine (CI): give them room.
describe('AppointmentDialog', {timeout: 30_000}, () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T07:00:00Z')});
  });

  it('says when an appointment is, or that it is not booked', () => {
    expect(appointmentWhen(testAppointment({date: MONDAY, start: '09:00', end: '09:30'}))).toBe('Monday 23 November, 09:00 to 09:30 with test-user');
    expect(appointmentWhen(testAppointment({staffUsername: null}))).toBe('Monday 23 November, 09:00 to 09:30');
    expect(appointmentWhen(testRequested())).toBe('Not booked yet');
  });

  it("shows a booked appointment's details, with links to its patient and pathway", async () => {
    const {user, router, onClose} = renderDialog(testAppointment({id: 'appt-1', notes: 'Bring the scan.'}));

    expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
    // Who it's for, as the patient banner shows them (hazard H-34).
    const banner = await dialog().findByRole('region', {name: 'Patient: Patient, Test'});
    expect(banner).toHaveTextContent('NHS No. 999 000 0018');
    expect(banner).toHaveTextContent(/Born 1 January 1980/);
    expect(dialog().getAllByText('Booked').some((e) => e.closest('.astryx-token'))).toBe(true);
    expect(dialog().getByText('Visit required')).toBeInTheDocument();
    expect(dialog().getByText('Monday 23 November, 09:00 to 09:30 with test-user')).toBeInTheDocument();
    // Requested and booked by the same person at the same time.
    expect(dialog().getAllByText('By test-admin, 10 Nov 2026, 10:00')).toHaveLength(2);
    expect(dialog().getByRole('textbox', {name: /Notes/})).toHaveValue('Bring the scan.');
    expect(dialog().queryByRole('button', {name: 'Attended'})).not.toBeInTheDocument();

    await user.click(dialog().getByRole('link', {name: 'Knee review: Assessment'}));
    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways/pathway-1'));
    expect(onClose).toHaveBeenCalled();
  });

  it('says an appointment was recorded after the event, by whom and when, rather than booked', async () => {
    const walkIn = testAppointment({
      id: 'walk-in',
      status: 'Attended',
      isRecordedAfterEvent: true,
      createdAtUtc: '2026-11-20T11:00:00Z',
      createdByUsername: 'test-user',
      bookedAtUtc: '2026-11-20T11:00:00Z',
      bookedByUsername: 'test-user',
    });
    const {unmount} = renderDialog(walkIn);

    expect(await dialog().findByText('Recorded after the event')).toBeInTheDocument();
    expect(dialog().getAllByText('By test-user, 20 Nov 2026, 11:00').length).toBeGreaterThan(0);
    // A walk-in was never asked for.
    expect(dialog().queryByText('Requested')).not.toBeInTheDocument();
    unmount();

    // A visit waiting in Requires Appointment, recorded later: it was asked for first.
    renderDialog({...walkIn, isAutomatic: true, createdAtUtc: '2026-11-10T10:00:00Z'});
    expect(await dialog().findByText('Recorded after the event')).toBeInTheDocument();
    expect(dialog().getByText('Requested')).toBeInTheDocument();
    expect(dialog().getByText('Automatically, 10 Nov 2026, 10:00')).toBeInTheDocument();
  });

  it('saves its notes', async () => {
    withAnnouncements();
    const appointment = testAppointment({id: 'appt-1'});
    const {user, fetch} = renderDialog(appointment, {'PUT /api/appointments/appt-1/notes': () => json({...appointment, notes: 'Wheelchair access.'})});

    const save = await dialog().findByRole('button', {name: 'Save Notes'});
    expect(isDisabled(save)).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /Notes/}), ' Wheelchair access. ');
    await user.click(save);

    await expectAnnounced('Notes saved');
    expect(await bodyOf(fetch, 'PUT', '/api/appointments/appt-1/notes')).toEqual({notes: 'Wheelchair access.'});
  });

  it("shows a notes error on the field, and any other problem above", async () => {
    const appointment = testAppointment({id: 'appt-1'});
    let status = 400;
    const {user} = renderDialog(appointment, {
      'PUT /api/appointments/appt-1/notes': () =>
        status === 400 ? json({errors: {Notes: ['Notes are too long.']}}, 400) : json({title: 'The appointment was cancelled.'}, 409),
    });

    await user.type(await dialog().findByRole('textbox', {name: /Notes/}), 'x');
    await user.click(dialog().getByRole('button', {name: 'Save Notes'}));
    expect(await dialog().findByText('Notes are too long.')).toBeInTheDocument();

    status = 409;
    await user.click(dialog().getByRole('button', {name: 'Save Notes'}));
    expect(await dialog().findByText('The appointment was cancelled.')).toBeInTheDocument();
  });

  it("keeps what was typed, and says what's missing, when the API refuses for a permission", async () => {
    const appointment = testAppointment({id: 'appt-1'});
    const detail = "Your roles don't allow this. It needs the permission to: Request, book, move and cancel appointments.";
    const {user} = renderDialog(appointment, {
      'PUT /api/appointments/appt-1/notes': () => json({title: 'Permission needed', detail, code: 'permission_missing', permission: 'BookAppointments'}, 403),
    });

    await user.type(await dialog().findByRole('textbox', {name: /Notes/}), 'Ramp at the side door.');
    await user.click(dialog().getByRole('button', {name: 'Save Notes'}));

    expect(await dialog().findByText(detail)).toBeInTheDocument();
    expect(dialog().getByRole('textbox', {name: /Notes/})).toHaveValue('Ramp at the side door.');
  });

  it("shows someone who can't book (a manager) the appointment, but nothing to change it with", async () => {
    const appointment = testAppointment({id: 'appt-1', notes: 'Bring the scan.', canRecordOutcome: true});
    renderDialog(appointment, {}, undefined, managerUser);

    expect(await dialog().findByText('Monday 23 November, 09:00 to 09:30 with test-user')).toBeInTheDocument();
    expect(dialog().getByText('Bring the scan.')).toBeInTheDocument();
    expect(dialog().queryByRole('textbox', {name: /Notes/})).not.toBeInTheDocument();
    for (const name of ['Attended', 'Reschedule', 'Cancel Appointment', 'Save Notes'])
      expect(dialog().queryByRole('button', {name})).not.toBeInTheDocument();
  });

  it('records whether the patient came, once it has started', async () => {
    withAnnouncements();
    let appointment = testAppointment({id: 'appt-1', canRecordOutcome: true, canReschedule: false, canCancel: false});
    const {user, fetch} = renderDialog(appointment, {
      'GET /api/appointments/appt-1': () => json(appointment),
      'POST /api/appointments/appt-1/outcome': () => {
        appointment = {...appointment, status: 'DidNotAttend'};
        return json(appointment);
      },
    });

    await user.click(await dialog().findByRole('button', {name: 'Did not attend'}));

    await expectAnnounced('Marked Did not attend');
    expect(await bodyOf(fetch, 'POST', '/api/appointments/appt-1/outcome')).toEqual({outcome: 'DidNotAttend'});
    await waitFor(() => expect(isDisabled(dialog().getByRole('button', {name: 'Did not attend'}))).toBe(true));
    expect(dialog().getByRole('button', {name: 'Book Another'})).toBeInTheDocument();
  });

  it('marks an appointment Attended, and says when that fails', async () => {
    const appointment = testAppointment({id: 'appt-1', canRecordOutcome: true});
    const {user} = renderDialog(appointment, {'POST /api/appointments/appt-1/outcome': () => json({}, 500)});

    await user.click(await dialog().findByRole('button', {name: 'Attended'}));
    expect(await dialog().findByText('The appointment could not be changed. Please try again.')).toBeInTheDocument();
  });

  it('cancels with a reason, after asking', async () => {
    withAnnouncements();
    const appointment = testAppointment({id: 'appt-1'});
    const {user, fetch} = renderDialog(appointment, {
      'POST /api/appointments/appt-1/cancel': () => json({...appointment, status: 'Cancelled', cancelReason: 'Patient unwell.'}),
    });

    await user.click(await dialog().findByRole('button', {name: 'Cancel Appointment'}));
    const confirm = await screen.findByRole('heading', {name: 'Cancel Appointment'});
    expect(confirm).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Cancel Appointment'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /Reason/}), 'Patient unwell.');
    await user.click(dialog().getByRole('button', {name: 'Cancel Appointment'}));

    await expectAnnounced('Appointment cancelled');
    expect(await bodyOf(fetch, 'POST', '/api/appointments/appt-1/cancel')).toEqual({reason: 'Patient unwell.'});
    expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
  });

  it('offers to tell the patient of a cancellation while messages are on, ticked when they chose a channel', async () => {
    const appointment = testAppointment({id: 'appt-1', patientReminderChannel: 'Email'});
    const {user, fetch} = renderDialog(appointment, {
      'GET /api/appointments/patient-messages/status': () => json(MESSAGES_ON),
      'GET /api/appointments/appt-1/messages': () => json([]),
      'POST /api/appointments/appt-1/cancel': () => json({...appointment, status: 'Cancelled'}),
    });

    await user.click(await dialog().findByRole('button', {name: 'Cancel Appointment'}));
    await screen.findByRole('heading', {name: 'Cancel Appointment'});
    const tell = dialog().getByRole('checkbox', {name: 'Tell the patient by text or email'});
    expect(tell).toBeChecked();
    await user.type(dialog().getByRole('textbox', {name: /Reason/}), 'Clinic closed.');
    await user.click(dialog().getByRole('button', {name: 'Cancel Appointment'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(true));
    expect(await bodyOf(fetch, 'POST', '/api/appointments/appt-1/cancel')).toEqual({reason: 'Clinic closed.', tellPatient: true});
  });

  it("doesn't tick Tell the patient for one who chose no reminders, and sends what was chosen", async () => {
    const appointment = testAppointment({id: 'appt-1', patientReminderChannel: 'None'});
    const {user, fetch} = renderDialog(appointment, {
      'GET /api/appointments/patient-messages/status': () => json(MESSAGES_ON),
      'POST /api/appointments/appt-1/cancel': () => json({...appointment, status: 'Cancelled'}),
    });

    await user.click(await dialog().findByRole('button', {name: 'Cancel Appointment'}));
    await screen.findByRole('heading', {name: 'Cancel Appointment'});
    expect(dialog().getByRole('checkbox', {name: 'Tell the patient by text or email'})).not.toBeChecked();
    await user.type(dialog().getByRole('textbox', {name: /Reason/}), 'Clinic closed.');
    await user.click(dialog().getByRole('button', {name: 'Cancel Appointment'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(true));
    expect(await bodyOf(fetch, 'POST', '/api/appointments/appt-1/cancel')).toEqual({reason: 'Clinic closed.', tellPatient: false});
  });

  describe('messages to the patient', () => {
    const reminder = {
      id: 'message-1',
      appointmentId: 'appt-1',
      kind: 'AppointmentReminder',
      channel: 'Text',
      destination: '+447700900123',
      body: 'Reminder: your appointment at Main site is on Mon 23 Nov at 09:00.',
      subject: null,
      appointmentStartUtc: '2026-11-23T09:00:00Z',
      status: 'Delivered',
      createdAtUtc: '2026-11-21T09:00:00Z',
      sentAtUtc: '2026-11-21T09:00:00Z',
      statusAtUtc: '2026-11-21T09:01:00Z',
    };
    const notSent = {
      ...reminder,
      id: 'message-2',
      channel: 'Email',
      destination: '',
      body: '',
      status: 'NotSent',
      statusReason: 'NoEmail',
      sentAtUtc: null,
      statusAtUtc: null,
    };

    it('lists each message: what it was, how and where it went, where it stands, when, and its text', async () => {
      renderDialog(testAppointment({id: 'appt-1'}), {
        'GET /api/appointments/patient-messages/status': () => json(MESSAGES_ON),
        'GET /api/appointments/appt-1/messages': () => json([reminder, notSent]),
      });

      const list = await dialog().findByRole('list', {name: 'Messages to the patient'});
      const items = within(list).getAllByRole('listitem');
      expect(items[0]).toHaveTextContent('Reminder: Text to 07700 900123');
      expect(items[0]).toHaveTextContent('Delivered · 21 Nov 2026, 09:01');
      expect(items[0]).toHaveTextContent(reminder.body);
      expect(items[1]).toHaveTextContent('Reminder: Email');
      expect(items[1]).toHaveTextContent('No reminder: no consent or contact (no email address)');
    });

    it('says when there are none while messages are on, and shows nothing while they are off', async () => {
      const {unmount} = renderDialog(testAppointment({id: 'appt-1'}), {
        'GET /api/appointments/patient-messages/status': () => json(MESSAGES_ON),
        'GET /api/appointments/appt-1/messages': () => json([]),
      });
      expect(await dialog().findByText('No messages.')).toBeInTheDocument();
      unmount();

      const {fetch} = renderDialog(testAppointment({id: 'appt-1'}), {
        'GET /api/appointments/patient-messages/status': () => json({...MESSAGES_ON, mode: 'Off', isOn: false}),
        'GET /api/appointments/appt-1/messages': () => json([]),
      });
      await waitFor(() => expect(calls(fetch)).toContain('GET /api/appointments/appt-1/messages'));
      expect(await dialog().findByText('Monday 23 November, 09:00 to 09:30 with test-user')).toBeInTheDocument();
      expect(dialog().queryByText('No messages.')).not.toBeInTheDocument();
      expect(dialog().queryByRole('heading', {name: 'Messages to the patient'})).not.toBeInTheDocument();
    });

    it("lists messages sent before they were turned off, and doesn't ask for an appointment with no time", async () => {
      const {unmount} = renderDialog(testAppointment({id: 'appt-1'}), {'GET /api/appointments/appt-1/messages': () => json([reminder])});
      expect(await dialog().findByRole('list', {name: 'Messages to the patient'})).toBeInTheDocument();
      unmount();

      const {fetch} = renderDialog(testRequested({id: 'req-1'}));
      expect(await dialog().findByText('Not booked yet')).toBeInTheDocument();
      expect(calls(fetch)).not.toContain('GET /api/appointments/req-1/messages');
    });
  });

  describe('did not attend follow-up', () => {
    const missed = testAppointment({
      id: 'appt-1',
      status: 'DidNotAttend',
      canCancel: false,
      canReschedule: false,
      canRecordOutcome: true,
      canRecordDnaFollowUp: true,
    });

    it('records a follow-up, saying it changes nothing else, with a detail required for No further appointment', async () => {
      withAnnouncements();
      let shown: Appointment = missed;
      const {user, fetch} = renderDialog(missed, {
        'GET /api/appointments/appt-1': () => json(shown),
        'POST /api/appointments/appt-1/dna-follow-up': () => {
          shown = {...missed, dnaFollowUp: 'NoFurtherAppointment', dnaFollowUpDetail: 'Dr Test: discharged to GP.', dnaFollowUpByUsername: 'test-user', dnaFollowUpAtUtc: '2026-11-23T07:00:00Z'};
          return json(shown);
        },
      });

      await user.click(await dialog().findByRole('button', {name: 'Record Follow-up'}));
      expect(await screen.findByRole('heading', {name: 'Record Follow-up'})).toBeInTheDocument();
      expect(dialog().getByText(/MedApp never discharges a patient or changes a pathway because of a missed appointment/)).toBeInTheDocument();
      // Who the patient is, and which appointment was missed.
      expect(await dialog().findByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
      expect(dialog().getByText('Mon 23 Nov 2026, 09:00 to 09:30 with test-user')).toBeInTheDocument();
      // Nothing is chosen to start with.
      const confirm = dialog().getByRole('button', {name: 'Record Follow-up'});
      expect(dialog().getAllByRole('radio').some((r) => (r as HTMLInputElement).checked)).toBe(false);
      expect(isDisabled(confirm)).toBe(true);
      await user.click(dialog().getByRole('radio', {name: 'No further appointment'}));
      expect(isDisabled(confirm)).toBe(true);
      expect(dialog().getByText("Say who decided there's to be no further appointment, and why.")).toBeInTheDocument();
      await user.type(dialog().getByRole('textbox', {name: /Detail/}), ' Dr Test: discharged to GP. ');
      await user.click(confirm);

      await expectAnnounced('Follow-up recorded: No further appointment');
      expect(await bodyOf(fetch, 'POST', '/api/appointments/appt-1/dna-follow-up')).toEqual({decision: 'NoFurtherAppointment', detail: 'Dr Test: discharged to GP.'});
      // Back on the appointment, with the follow-up recorded.
      expect(await dialog().findByText('No further appointment, by test-user, 23 Nov 2026, 07:00')).toBeInTheDocument();
      expect(dialog().getByText('Dr Test: discharged to GP.')).toBeInTheDocument();
    });

    it("shows the API's refusals: a 400 on its field, anything else in its words", async () => {
      let status = 400;
      const {user} = renderDialog(missed, {
        'POST /api/appointments/appt-1/dna-follow-up': () =>
          status === 400 ? json({errors: {Detail: ['Detail must be 500 characters or fewer.']}}, 400) : json({title: 'The record is restricted.'}, 409),
      });

      await user.click(await dialog().findByRole('button', {name: 'Record Follow-up'}));
      await user.click(await dialog().findByRole('radio', {name: 'Rebook'}));
      await user.click(dialog().getByRole('button', {name: 'Record Follow-up'}));
      expect(await dialog().findByText('Detail must be 500 characters or fewer.')).toBeInTheDocument();

      status = 409;
      await user.click(dialog().getByRole('button', {name: 'Record Follow-up'}));
      expect(await dialog().findByText('The record is restricted.')).toBeInTheDocument();
      // Called off: back to the appointment.
      await user.click(dialog().getByRole('button', {name: 'Cancel'}));
      expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
    });

    it("isn't offered without the API's flag, or to someone who can't book", async () => {
      const {unmount} = renderDialog({...missed, canRecordDnaFollowUp: false});
      expect(await dialog().findByRole('button', {name: 'Book Another'})).toBeInTheDocument();
      expect(dialog().queryByRole('button', {name: 'Record Follow-up'})).not.toBeInTheDocument();
      unmount();

      renderDialog(missed, {}, undefined, managerUser);
      expect(await dialog().findByText('Monday 23 November, 09:00 to 09:30 with test-user')).toBeInTheDocument();
      expect(dialog().queryByRole('button', {name: 'Record Follow-up'})).not.toBeInTheDocument();
    });
  });

  it('goes back to the appointment when cancelling fails or is called off', async () => {
    const appointment = testAppointment({id: 'appt-1'});
    const {user} = renderDialog(appointment, {'POST /api/appointments/appt-1/cancel': () => json({title: 'It has already happened.'}, 409)});

    await user.click(await dialog().findByRole('button', {name: 'Cancel Appointment'}));
    await user.click(await dialog().findByRole('button', {name: 'Keep It'}));
    await user.click(await dialog().findByRole('button', {name: 'Cancel Appointment'}));
    await user.type(await dialog().findByRole('textbox', {name: /Reason/}), 'No longer needed.');
    await user.click(dialog().getByRole('button', {name: 'Cancel Appointment'}));

    expect(await dialog().findByText('It has already happened.')).toBeInTheDocument();
  });

  describe('entered in error', () => {
    /** A walk-in test-user recorded, which the API says can be withdrawn. */
    const walkIn = testAppointment({
      id: 'walk-in',
      status: 'Attended',
      isRecordedAfterEvent: true,
      createdAtUtc: '2026-11-20T11:00:00Z',
      bookedAtUtc: '2026-11-20T11:00:00Z',
      bookedByUsername: 'test-user',
      canCancel: false,
      canReschedule: false,
      canRecordOutcome: true,
      canMarkEnteredInError: true,
      canRecorderMarkEnteredInError: true,
    });

    it("withdraws a walk-in its recorder entered in error, with a reason, saying what happens to the stage's visit", async () => {
      withAnnouncements();
      let shown: Appointment = walkIn;
      const withdrawn = {...walkIn, status: 'EnteredInError', cancelReason: 'Recorded on the wrong patient.', canRecordOutcome: false, canMarkEnteredInError: false};
      const stageMessage = 'Its visit is back in Requires Appointment.';
      const {user, fetch} = renderDialog(walkIn, {
        'GET /api/appointments/walk-in': () => json(shown),
        'POST /api/appointments/walk-in/entered-in-error': () => {
          shown = withdrawn;
          return json({appointment: withdrawn, visitRequestedAgain: true, stageMessage});
        },
      });

      await user.click(await dialog().findByRole('button', {name: 'Entered in Error'}));
      expect(await screen.findByRole('heading', {name: 'Mark Entered in Error'})).toBeInTheDocument();
      expect(dialog().getByText(/withdraws the record of a visit that didn't happen, or that's on the wrong patient/)).toBeInTheDocument();
      expect(dialog().getByText(/stays in the appointment's history, and it can't be undone/)).toBeInTheDocument();
      expect(isDisabled(dialog().getByRole('button', {name: 'Mark Entered in Error'}))).toBe(true);
      await user.type(dialog().getByRole('textbox', {name: /Reason/}), ' Recorded on the wrong patient. ');
      await user.click(dialog().getByRole('button', {name: 'Mark Entered in Error'}));

      await expectAnnounced(`Marked entered in error. ${stageMessage}`);
      expect(await bodyOf(fetch, 'POST', '/api/appointments/walk-in/entered-in-error')).toEqual({reason: 'Recorded on the wrong patient.'});
      expect(await dialog().findByText(stageMessage)).toBeInTheDocument();
      // It shows as entered in error, with why, and nothing more can be done to it.
      expect(dialog().getAllByText('Entered in error').some((e) => e.closest('.astryx-token'))).toBe(true);
      expect(dialog().getByText('Why entered in error')).toBeInTheDocument();
      expect(dialog().getByText('Recorded on the wrong patient.')).toBeInTheDocument();
      expect(dialog().queryByRole('button', {name: 'Entered in Error'})).not.toBeInTheDocument();
      expect(dialog().queryByRole('button', {name: 'Attended'})).not.toBeInTheDocument();
    });

    it('announces it with no stage message, goes back when called off, and shows a refusal', async () => {
      withAnnouncements();
      let status = 200;
      const {user} = renderDialog(walkIn, {
        'POST /api/appointments/walk-in/entered-in-error': () =>
          status === 200 ? json({appointment: walkIn, visitRequestedAgain: false, stageMessage: null}) : json({title: 'It was recorded over 30 days ago.'}, 409),
      });

      await user.click(await dialog().findByRole('button', {name: 'Entered in Error'}));
      await user.click(await dialog().findByRole('button', {name: 'Keep It'}));
      expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
      await user.click(dialog().getByRole('button', {name: 'Entered in Error'}));
      await user.type(await dialog().findByRole('textbox', {name: /Reason/}), 'Never happened.');
      await user.click(dialog().getByRole('button', {name: 'Mark Entered in Error'}));
      await expectAnnounced('Marked entered in error');

      status = 409;
      await user.click(await dialog().findByRole('button', {name: 'Entered in Error'}));
      await user.type(await dialog().findByRole('textbox', {name: /Reason/}), 'Never happened.');
      await user.click(dialog().getByRole('button', {name: 'Mark Entered in Error'}));
      expect(await dialog().findByText('It was recorded over 30 days ago.')).toBeInTheDocument();
    });

    it('is offered only to whoever recorded it, or an admin, and only when the API allows it', async () => {
      const someoneElses = {...walkIn, bookedByUsername: 'j.smith'};
      const staff = renderDialog(someoneElses);
      expect(await dialog().findByRole('button', {name: 'Attended'})).toBeInTheDocument();
      expect(dialog().queryByRole('button', {name: 'Entered in Error'})).not.toBeInTheDocument();
      staff.unmount();

      const admin = renderDialog(someoneElses, {}, undefined, adminUser);
      expect(await dialog().findByRole('button', {name: 'Entered in Error'})).toBeInTheDocument();
      admin.unmount();

      const none = renderDialog({...walkIn, canMarkEnteredInError: false});
      expect(await dialog().findByRole('button', {name: 'Attended'})).toBeInTheDocument();
      expect(dialog().queryByRole('button', {name: 'Entered in Error'})).not.toBeInTheDocument();
      none.unmount();

      // Over 30 days on, only an admin can, not whoever recorded it.
      const old = {...walkIn, canRecorderMarkEnteredInError: false};
      const recorder = renderDialog(old);
      expect(await dialog().findByRole('button', {name: 'Attended'})).toBeInTheDocument();
      expect(dialog().queryByRole('button', {name: 'Entered in Error'})).not.toBeInTheDocument();
      recorder.unmount();

      renderDialog(old, {}, undefined, adminUser);
      expect(await dialog().findByRole('button', {name: 'Entered in Error'})).toBeInTheDocument();
    });
  });

  it('reschedules in the booking dialog, and comes back', async () => {
    const {user} = renderDialog(testAppointment({id: 'appt-1'}));

    await user.click(await dialog().findByRole('button', {name: 'Reschedule'}));
    expect(await screen.findByRole('heading', {name: 'Reschedule Appointment'})).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
  });

  it('reschedules in the steps of the calendar it was opened from', async () => {
    const {user} = renderDialog(testAppointment({id: 'appt-1'}), {}, 5);

    await user.click(await dialog().findByRole('button', {name: 'Reschedule'}));
    const morning = await dialog().findByRole('group', {name: 'Morning times on Monday 23 November'});
    // 5-minute steps around its own time (09:00), as the calendar's 5-minute rows have, and only there.
    expect(within(morning).getByRole('button', {name: '09:05'})).toBeInTheDocument();
    expect(within(morning).getByRole('button', {name: '08:55'})).toBeInTheDocument();
    expect(within(morning).queryByRole('button', {name: '10:05'})).not.toBeInTheDocument();
    // Choosing another time leaves them there, so nothing moves under the pointer.
    await user.click(within(morning).getByRole('button', {name: '10:00'}));
    expect(within(morning).getByRole('button', {name: '09:05'})).toBeInTheDocument();
    expect(within(morning).queryByRole('button', {name: '10:05'})).not.toBeInTheDocument();
  });

  it('books a Requested appointment, which says who asked for it', async () => {
    const {user} = renderDialog(testRequested({id: 'req-1', isAutomatic: true, notes: null}));

    expect(await dialog().findByText('Requires appointment')).toBeInTheDocument();
    expect(dialog().getByText('Not booked yet')).toBeInTheDocument();
    expect(dialog().getByText('Automatically, 10 Nov 2026, 10:00')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Book'}));
    expect(await screen.findByRole('heading', {name: 'Book Visit'})).toBeInTheDocument();
  });

  it('shows why a cancelled one was cancelled, its notes read-only, and books another', async () => {
    const appointment = testAppointment({
      id: 'appt-1',
      status: 'Cancelled',
      cancelReason: 'Patient moved away.',
      notes: 'Ground floor.',
      canEditNotes: false,
      canCancel: false,
      canReschedule: false,
      pathwayId: null,
      pathwayTitle: null,
      stageId: null,
      stageName: null,
      outcomeAtUtc: '2026-11-12T10:00:00Z',
      outcomeByUsername: 'test-user',
    });
    const onClose = vi.fn();
    function Harness() {
      const [shown, setShown] = useState<Appointment | null>(appointment);
      return (
        <AppointmentDialog
          appointment={shown}
          onClose={() => {
            onClose();
            setShown(null);
          }}
        />
      );
    }
    mockApi({
      'GET /api/appointments/appt-1': () => json(appointment),
      'GET /api/calendar/staff': () => json([STAFF_ME]),
      'GET /api/calendar': (request) => json(testCalendar({from: new URL(request.url).searchParams.get('From')!, dayCount: 5, today: MONDAY})),
      'GET /api/pathways/patient-pathways/search': () => json({items: []}),
      'GET /api/patients/patient-1': () => json(TEST_PATIENT),
    });
    const {user} = renderWithApp(<Harness />, {user: staffUser});

    expect(await dialog().findByText('Patient moved away.')).toBeInTheDocument();
    expect(dialog().getByText('Ground floor.')).toBeInTheDocument();
    expect(dialog().getByText('Appointment', {selector: '.astryx-metadata-list *'})).toBeInTheDocument();
    expect(dialog().getByText('By test-user, 12 Nov 2026, 10:00')).toBeInTheDocument();
    await user.click(await dialog().findByRole('button', {name: 'Book Another'}));
    expect(await screen.findByRole('heading', {name: 'Book Appointment'})).toBeInTheDocument();
    // The same patient, with their banner.
    expect(dialog().getByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
    expect(dialog().queryByRole('combobox', {name: /Patient/})).not.toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    // The footer's Close (the header has one too).
    await user.click((await dialog().findAllByRole('button', {name: 'Close'})).at(-1)!);
    expect(onClose).toHaveBeenCalled();
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('saves notes with Ctrl+S, and only when they changed', async () => {
    const appointment = testAppointment({id: 'appt-1'});
    const {user, fetch} = renderDialog(appointment, {'PUT /api/appointments/appt-1/notes': () => json(appointment)});

    const notes = await dialog().findByRole('textbox', {name: /Notes/});
    notes.focus();
    fireEvent.keyDown(notes, {key: 's', ctrlKey: true});
    expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'PUT')).toBe(false);
    await user.type(notes, 'Hearing aid.');
    notes.focus();
    fireEvent.keyDown(notes, {key: 's', ctrlKey: true});
    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'PUT')).toBe(true));
  });

  it("lets a restricted patient's appointment be cancelled, but nothing else, and says why", async () => {
    const appointment = testAppointment({id: 'appt-1', canReschedule: false, canEditNotes: false, canRecordOutcome: false, notes: 'Ground floor.'});
    renderDialog(appointment, {
      'GET /api/patients/patient-1': () => json({...TEST_PATIENT, isRestricted: true, restrictionReason: 'Synthetic accuracy check'}),
    });

    const banner = await dialog().findByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('Restricted')).toBeInTheDocument();
    expect(dialog().getByText(/Restricted record: .* an appointment cancelled/)).toBeInTheDocument();
    expect(dialog().getByRole('button', {name: 'Cancel Appointment'})).toBeInTheDocument();
    expect(dialog().queryByRole('button', {name: 'Reschedule'})).not.toBeInTheDocument();
    expect(dialog().queryByRole('textbox', {name: /Notes/})).not.toBeInTheDocument();
    expect(dialog().getByText('Ground floor.')).toBeInTheDocument();
  });

  it("doesn't offer to book another for a restricted or anonymised patient", async () => {
    const appointment = testAppointment({id: 'appt-1', status: 'Cancelled', canCancel: false, canReschedule: false, canEditNotes: false});
    renderDialog(appointment, {'GET /api/patients/patient-1': () => json({...TEST_PATIENT, isAnonymised: true})});

    expect(await dialog().findByText(/This record has been anonymised/)).toBeInTheDocument();
    expect(dialog().queryByRole('button', {name: 'Book Another'})).not.toBeInTheDocument();
  });
});
