import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {STAFF_ME, STAFF_OTHER, TEST_PATIENT, testAppointment, testBlock, testCalendar, testMeeting, testRequested, ukTime} from '../../test/calendar';
import {calls, openAlertDetails, expectAnnounced, isDisabled, json, mockApi, openDialog, pageOf, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {BookAppointmentDialog, shownStarts, type BookAppointmentDialogProps} from './BookAppointmentDialog';

const MONDAY = '2026-11-23';
const STAFF = [STAFF_ME, STAFF_OTHER];

/** Monday 23 November 2026, 07:00 UK time, before the working day starts. */
function atMondayMorning(time = '2026-11-23T07:00:00Z') {
  vi.useFakeTimers({toFake: ['Date'], now: new Date(time)});
}

function calendarFor(request: Request, extra: Parameters<typeof testCalendar>[0] extends infer T ? Partial<T> : never = {}) {
  const url = new URL(request.url);
  const from = url.searchParams.get('From')!;
  const staffId = url.searchParams.get('StaffUserId');
  const staff = STAFF.find((s) => s.userId === staffId) ?? STAFF_ME;
  return json(testCalendar({from, dayCount: 5, today: MONDAY, staff, ...extra}));
}

function renderDialog(props: Partial<BookAppointmentDialogProps> = {}, handlers: Parameters<typeof mockApi>[0] = {}) {
  const onClose = vi.fn();
  const onSaved = vi.fn();
  const fetch = mockApi({
    'GET /api/calendar/staff': () => json(STAFF),
    'GET /api/calendar': (request) => calendarFor(request, {blocks: [testBlock(MONDAY, '08:00', '09:00')]}),
    'GET /api/patients/patient-1': () => json(TEST_PATIENT),
    ...handlers,
  });
  const result = renderWithApp(
    <SaveShortcutProvider>
      <BookAppointmentDialog isOpen onClose={onClose} onSaved={onSaved} {...props} />
    </SaveShortcutProvider>,
    {user: staffUser},
  );
  return {...result, fetch, onClose, onSaved};
}

const dialog = () => within(openDialog()!);
const bodyOf = async (fetch: ReturnType<typeof mockApi>, method: string, path: string) => {
  const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method && new URL(r.url).pathname === path)!;
  return request.clone().json();
};

// Slow under coverage on a loaded machine (CI): give it room.
describe('BookAppointmentDialog', {timeout: 30_000}, () => {
  it('books a visit for a known stage in a free time, with the signed-in user by default', {timeout: 20000}, async () => {
    atMondayMorning();
    withAnnouncements();
    const booked = testAppointment({date: MONDAY, start: '09:00', end: '09:30'});
    const {fetch, user, onSaved, onClose} = renderDialog(
      {patient: TEST_PATIENT, procedure: {id: 'procedure-1', title: 'Knee review'}, stage: {id: 'stage-1', name: 'Assessment'}},
      {'POST /api/appointments': () => json(booked)},
    );

    expect(await screen.findByRole('heading', {name: 'Book Visit'})).toBeInTheDocument();
    // Who it's for, as the patient banner shows them, so staff can check (hazard H-34).
    const banner = dialog().getByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('NHS No. 999 000 0018')).toBeInTheDocument();
    expect(within(banner).getByText(/^Born 1 January 1980/)).toBeInTheDocument();
    expect(dialog().getByText('Knee review: Assessment')).toBeInTheDocument();
    // The first working day with a free time is chosen; the morning's first hour is taken by a block.
    const monday = await dialog().findByRole('button', {name: 'Mon 23, 35 free times'});
    expect(monday).toHaveAttribute('aria-pressed', 'true');
    const morning = dialog().getByRole('group', {name: 'Morning times on Monday 23 November'});
    expect(within(morning).queryByRole('button', {name: '08:45'})).not.toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Book'}))).toBe(true);

    await user.click(within(morning).getByRole('button', {name: '09:00'}));
    expect(dialog().getByText('Monday 23 November, 09:00 to 09:30 with test-user')).toBeInTheDocument();
    expect(dialog().queryByRole('button', {name: 'Add to Requires Appointment'})).not.toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Book'}));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(booked));
    expect(onClose).toHaveBeenCalled();
    await expectAnnounced('Appointment booked');
    expect(await bodyOf(fetch, 'POST', '/api/appointments')).toEqual({
      patientId: 'patient-1',
      procedureId: 'procedure-1',
      stageId: 'stage-1',
      staffUserId: STAFF_ME.userId,
      startUtc: '2026-11-23T09:00:00.000Z',
      endUtc: '2026-11-23T09:30:00.000Z',
      notes: null,
    });
  });

  it('offers the time of a meeting the member of staff declined, but not of one they are going to', async () => {
    atMondayMorning();
    const meeting = (start: string, end: string, reply: string) =>
      testMeeting({id: `m-${start}`, date: MONDAY, start, end, attendees: [{userId: STAFF_ME.userId, username: STAFF_ME.username, reply}]});
    renderDialog({patient: TEST_PATIENT}, {
      'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
      'GET /api/calendar': (request) => calendarFor(request, {meetings: [meeting('09:00', '10:00', 'Declined'), meeting('11:00', '12:00', 'Accepted')]}),
    });

    await screen.findByRole('dialog');
    const morning = await dialog().findByRole('group', {name: 'Morning times on Monday 23 November'});
    expect(within(morning).getByRole('button', {name: '09:00'})).toBeInTheDocument();
    expect(within(morning).getByRole('button', {name: '09:30'})).toBeInTheDocument();
    expect(within(morning).queryByRole('button', {name: '11:00'})).not.toBeInTheDocument();
    expect(within(morning).queryByRole('button', {name: '11:30'})).not.toBeInTheDocument();
  });

  it('starts from a time picked on the calendar, with its length and member of staff', async () => {
    atMondayMorning();
    const {user, fetch} = renderDialog(
      {
        patient: TEST_PATIENT,
        staffUserId: STAFF_OTHER.userId,
        startUtc: ukTime('2026-11-25', '14:00'),
        endUtc: ukTime('2026-11-25', '14:45'),
      },
      {
        'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
        'POST /api/appointments': () => json(testAppointment()),
      },
    );

    await screen.findByRole('dialog');
    expect(await dialog().findByText('Wednesday 25 November, 14:00 to 14:45 with b.staff')).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Length/})).toHaveTextContent('45 minutes');
    expect(dialog().getByRole('combobox', {name: /With/})).toHaveTextContent('b.staff');
    expect(calls(fetch)).toContain(`GET /api/calendar?StaffUserId=${STAFF_OTHER.userId}&From=2026-11-23&To=2026-11-27`);

    await user.type(dialog().getByRole('textbox', {name: /Notes/}), '  Bring the scan.  ');
    await user.click(dialog().getByRole('button', {name: 'Book'}));
    await waitFor(async () =>
      expect(await bodyOf(fetch, 'POST', '/api/appointments')).toMatchObject({
        staffUserId: STAFF_OTHER.userId,
        startUtc: '2026-11-25T14:00:00.000Z',
        endUtc: '2026-11-25T14:45:00.000Z',
        notes: 'Bring the scan.',
        procedureId: null,
        stageId: null,
      }),
    );
  });

  it('lists times every 15 minutes, with 5-minute steps only around the time picked on a calendar in 5-minute rows', async () => {
    atMondayMorning();
    const {user} = renderDialog({
      patient: TEST_PATIENT,
      startUtc: ukTime('2026-11-25', '10:05'),
      endUtc: ukTime('2026-11-25', '10:20'),
      stepMinutes: 5,
    }, {'GET /api/procedures/patient-procedures/search': () => json(pageOf([]))});

    await screen.findByRole('dialog');
    expect(await dialog().findByText('Wednesday 25 November, 10:05 to 10:20 with test-user')).toBeInTheDocument();
    // The day still counts its quarter hours, not every 5 minutes.
    expect(dialog().getByRole('button', {name: 'Wed 25, 40 free times'})).toBeInTheDocument();
    const morning = dialog().getByRole('group', {name: 'Morning times on Wednesday 25 November'});
    // Astryx's toggle buttons hold their label's text twice: the first five characters are the time.
    const times = within(morning).getAllByRole('button').map((b) => b.textContent!.slice(0, 5));
    expect(times.filter((t) => t!.startsWith('09:') || t!.startsWith('10:'))).toEqual(['09:00', '09:15', '09:30', '09:45', '09:55', '10:00', '10:05', '10:10', '10:15', '10:30', '10:45']);
    // 16 quarter hours from 08:00, and the three finer times around 10:05.
    expect(times).toHaveLength(16 + 3);

    // Choosing another time leaves them where they are, so no button moves under the pointer.
    await user.click(within(morning).getByRole('button', {name: '11:30'}));
    expect(within(morning).getAllByRole('button').map((b) => b.textContent!.slice(0, 5))).toEqual(times);
    expect(within(morning).getByRole('button', {name: '11:30'})).toHaveAttribute('aria-pressed', 'true');
    expect(within(morning).queryByRole('button', {name: '11:25'})).not.toBeInTheDocument();
  });

  it('shows only quarter hours without a finer step or a chosen time', () => {
    const quarters = [ukTime(MONDAY, '10:00'), ukTime(MONDAY, '10:15')];
    const fine = [ukTime(MONDAY, '10:00'), ukTime(MONDAY, '10:05'), ukTime(MONDAY, '10:10'), ukTime(MONDAY, '10:15')];
    expect(shownStarts(quarters, undefined, ukTime(MONDAY, '10:00'))).toEqual(quarters);
    expect(shownStarts(quarters, fine, undefined)).toEqual(quarters);
    expect(shownStarts(quarters, fine, ukTime(MONDAY, '10:15'))).toEqual([ukTime(MONDAY, '10:00'), ukTime(MONDAY, '10:05'), ukTime(MONDAY, '10:10'), ukTime(MONDAY, '10:15')]);
  });

  it('offers an odd preselected length alongside the usual ones, and forgets the time when the length changes', async () => {
    atMondayMorning();
    const {user} = renderDialog(
      {patient: TEST_PATIENT, startUtc: ukTime(MONDAY, '17:00'), endUtc: ukTime(MONDAY, '17:20')},
      {'GET /api/procedures/patient-procedures/search': () => json(pageOf([]))},
    );

    await screen.findByRole('dialog');
    expect(await dialog().findByText('Monday 23 November, 17:00 to 17:20 with test-user')).toBeInTheDocument();
    await user.click(dialog().getByRole('combobox', {name: /Length/}));
    expect(screen.getByRole('option', {name: '20 minutes'})).toBeInTheDocument();
    await user.click(screen.getByRole('option', {name: '1 hour 30 minutes'}));

    // 17:00 for an hour and a half would run past the end of the day.
    expect(dialog().getByText('Choose a time, or add it to Requires Appointment to book later.')).toBeInTheDocument();
    expect(within(dialog().getByRole('group', {name: /Afternoon times/})).queryByRole('button', {name: '17:00'})).not.toBeInTheDocument();
  });

  it('books a Requested appointment, its patient and stage fixed', async () => {
    atMondayMorning();
    withAnnouncements();
    const requested = testRequested({id: 'req-1'});
    const {user, fetch} = renderDialog(
      {appointment: requested, suggestedStaffUserId: STAFF_OTHER.userId},
      {'POST /api/appointments/req-1/book': () => json(testAppointment({id: 'req-1'}))},
    );

    expect(await screen.findByRole('heading', {name: 'Book Visit'})).toBeInTheDocument();
    expect(dialog().getByText('Knee review: Assessment')).toBeInTheDocument();
    // The appointment carries only the patient's name, so who they are is read from their record.
    expect(await dialog().findByRole('region', {name: 'Patient: Patient, Test'})).toHaveTextContent('NHS No. 999 000 0018');
    expect(calls(fetch)).toContain('GET /api/patients/patient-1');
    expect(dialog().queryByRole('textbox', {name: /Notes/})).not.toBeInTheDocument();
    expect(dialog().getByText('Choose a time.')).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /With/})).toHaveTextContent('b.staff'));

    await user.click(await dialog().findByRole('button', {name: '10:00'}));
    await user.click(dialog().getByRole('button', {name: 'Book'}));

    await expectAnnounced('Appointment booked');
    expect(await bodyOf(fetch, 'POST', '/api/appointments/req-1/book')).toEqual({
      staffUserId: STAFF_OTHER.userId,
      startUtc: '2026-11-23T10:00:00.000Z',
      endUtc: '2026-11-23T10:30:00.000Z',
      // Booking it, as the page saw it: if someone else booked it first, the API refuses rather than move theirs.
      expectedStatus: 'Requested',
    });
  });

  it('says so, keeps the dialog open and refreshes the lists when someone else booked the appointment first', async () => {
    atMondayMorning();
    const requested = testRequested({id: 'req-1'});
    const message = 'Someone else has already booked this appointment. Reload it to see when; reschedule it from there if it needs to move.';
    const {user, fetch, onClose, queryClient} = renderDialog(
      {appointment: requested},
      {'POST /api/appointments/req-1/book': () => json({title: message}, 409)},
    );
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    expect(await screen.findByRole('heading', {name: 'Book Visit'})).toBeInTheDocument();
    await user.click(await dialog().findByRole('button', {name: '10:00'}));
    await user.click(dialog().getByRole('button', {name: 'Book'}));

    expect(await dialog().findByText(message)).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
    expect(await bodyOf(fetch, 'POST', '/api/appointments/req-1/book')).toMatchObject({expectedStatus: 'Requested'});
    expect(invalidate).toHaveBeenCalledWith({queryKey: ['appointments']});
    expect(invalidate).toHaveBeenCalledWith({queryKey: ['calendar']});
    // The time chosen is cleared: what's free is read again.
    expect(dialog().getByText('Choose a time.')).toBeInTheDocument();
  });

  it('reschedules a Booked appointment, which may move within its own time', async () => {
    atMondayMorning();
    withAnnouncements();
    const booked = testAppointment({id: 'appt-1', date: MONDAY, start: '11:00', end: '11:30'});
    const {user, fetch} = renderDialog(
      {appointment: booked},
      {
        'GET /api/calendar': (request) => calendarFor(request, {appointments: [booked]}),
        'POST /api/appointments/appt-1/book': () => json(booked),
      },
    );

    expect(await screen.findByRole('heading', {name: 'Reschedule Appointment'})).toBeInTheDocument();
    expect(dialog().getByText('Monday 23 November, 11:00 to 11:30 with test-user')).toBeInTheDocument();
    await user.click(await dialog().findByRole('button', {name: '10:45'}));
    await user.click(dialog().getByRole('button', {name: 'Reschedule'}));

    await expectAnnounced('Appointment rescheduled');
    expect(await bodyOf(fetch, 'POST', '/api/appointments/appt-1/book')).toMatchObject({expectedStatus: 'Booked'});
  });

  it('finds a patient by name, then one of their procedures and its stage', async () => {
    atMondayMorning();
    const {user, fetch} = renderDialog(
      {},
      {
        'GET /api/patients/search': (request) => {
          const url = new URL(request.url);
          const first = url.searchParams.get('FirstName');
          const last = url.searchParams.get('LastName');
          const patient = {id: 'patient-9', firstName: 'Alex', lastName: 'Sample', dateOfBirth: '1980-03-14', nhsNumber: '9990000026'};
          return json(pageOf((first ?? '').startsWith('al') || (last ?? '').startsWith('sa') || first === 'Alex' ? [patient] : []));
        },
        'GET /api/procedures/patient-procedures/search': () =>
          json(pageOf([{id: 'procedure-9', title: 'Hip review', dueDate: '2026-12-10', currentStageName: 'Consent'}])),
        'GET /api/procedures/patient-procedures/procedure-9': () =>
          json({
            id: 'procedure-9',
            stages: [
              {id: 'stage-a', name: 'Referral', state: 'Completed'},
              {id: 'stage-b', name: 'Consent', state: 'InProgress', isCurrent: true, visitRequirement: 'Required'},
              {id: 'stage-c', name: 'Follow-up', state: 'NotStarted', visitRequirement: 'Suggested'},
              {id: 'stage-d', name: 'Discharge', state: 'NotStarted', visitRequirement: 'None'},
            ],
          }),
        'POST /api/appointments': () => json(testRequested()),
      },
    );

    expect(await screen.findByRole('heading', {name: 'Book Appointment'})).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Add to Requires Appointment'}))).toBe(true);
    await user.type(dialog().getByRole('combobox', {name: /Patient/}), 'al');
    // Each result with its date of birth and NHS number, to tell patients with the same name apart.
    expect(await screen.findByRole('option', {name: /Alex Sample/})).toHaveTextContent('Born 14 Mar 1980 · NHS No. 999 000 0026');
    expect(dialog().queryByRole('region', {name: /^Patient: /})).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', {name: /Alex Sample/}));
    // The chosen patient's banner heads the dialog, from their search result (no read of their record).
    expect(await dialog().findByRole('region', {name: 'Patient: Sample, Alex'})).toHaveTextContent('Born 14 March 1980');
    expect(calls(fetch)).not.toContain('GET /api/patients/patient-9');

    await user.click(await dialog().findByRole('combobox', {name: /For/}));
    await user.click(await screen.findByRole('option', {name: /Hip review/}));
    await user.click(await dialog().findByRole('combobox', {name: /Stage/}));
    expect(screen.queryByRole('option', {name: /Referral/})).not.toBeInTheDocument();
    expect(screen.getByRole('option', {name: /Follow-up/})).toHaveTextContent('Visit suggested');
    await user.click(screen.getByRole('option', {name: /Consent/}));
    expect(await screen.findByRole('heading', {name: 'Book Visit'})).toBeInTheDocument();

    await user.click(dialog().getByRole('button', {name: 'Add to Requires Appointment'}));
    await waitFor(async () =>
      expect(await bodyOf(fetch, 'POST', '/api/appointments')).toEqual({
        patientId: 'patient-9',
        procedureId: 'procedure-9',
        stageId: 'stage-b',
        notes: null,
      }),
    );
    expect(calls(fetch)).toContain('GET /api/patients/search?PageSize=10&FirstName=al');
    expect(calls(fetch)).toContain('GET /api/patients/search?PageSize=10&LastName=al');
  });

  it('searches a first and a last name when two words are typed, and can go back to no procedure', async () => {
    atMondayMorning();
    const {user, fetch} = renderDialog(
      {},
      {
        'GET /api/patients/search': () => json(pageOf([{id: 'patient-9', firstName: 'Alex', lastName: 'Sample'}])),
        'GET /api/procedures/patient-procedures/search': () => json(pageOf([{id: 'procedure-9', title: 'Hip review', dueDate: '2026-12-10'}])),
        'GET /api/procedures/patient-procedures/procedure-9': () => json({id: 'procedure-9', stages: []}),
      },
    );

    await screen.findByRole('dialog');
    await user.type(await dialog().findByRole('combobox', {name: /Patient/}), 'Alex Sam');
    // Without a date of birth or NHS number, the result says so.
    expect(await screen.findByRole('option', {name: /Alex Sample/})).toHaveTextContent('Date of birth not known · No NHS number');
    await user.click(await screen.findByRole('option', {name: /Alex Sample/}));
    expect(calls(fetch)).toContain('GET /api/patients/search?PageSize=10&FirstName=Alex&LastName=Sam');

    await user.click(await dialog().findByRole('combobox', {name: /For/}));
    await user.click(await screen.findByRole('option', {name: /Hip review/}));
    expect(await dialog().findByRole('combobox', {name: /Stage/})).toBeInTheDocument();
    await user.click(dialog().getByRole('combobox', {name: /For/}));
    await user.click(screen.getByRole('option', {name: 'Not for a procedure'}));
    expect(dialog().queryByRole('combobox', {name: /Stage/})).not.toBeInTheDocument();
  });

  it("shows the server's message when someone took the time, and refreshes the free times", async () => {
    atMondayMorning();
    const {user, fetch} = renderDialog(
      {patient: TEST_PATIENT, procedure: {id: 'procedure-1', title: 'Knee review'}},
      {
        'POST /api/appointments': () => json({title: 'That time overlaps an appointment, 09:00 to 09:30.'}, 409),
        'GET /api/procedures/patient-procedures/procedure-1': () => json({id: 'procedure-1', stages: []}),
      },
    );

    await screen.findByRole('dialog');
    await user.click(await dialog().findByRole('button', {name: '09:00'}));
    await user.click(dialog().getByRole('button', {name: 'Book'}));

    expect(await dialog().findByText('That time overlaps an appointment, 09:00 to 09:30.')).toBeInTheDocument();
    expect(dialog().getByText('Choose a time, or add it to Requires Appointment to book later.')).toBeInTheDocument();
    await waitFor(() => expect(calls(fetch).filter((c) => c.startsWith('GET /api/calendar?'))).toHaveLength(2));
  });

  it('shows validation messages on their fields, and the rest above', async () => {
    atMondayMorning();
    const {user} = renderDialog(
      {patient: TEST_PATIENT},
      {
        'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
        'POST /api/appointments': () =>
          json({title: 'One or more validation errors occurred.', errors: {Notes: ['Notes are too long.'], StartUtc: ['Not on the hour.']}}, 400),
      },
    );

    await screen.findByRole('dialog');
    await user.click(await dialog().findByRole('button', {name: '09:00'}));
    await user.click(dialog().getByRole('button', {name: 'Book'}));

    expect(await dialog().findByText('Notes are too long.')).toBeInTheDocument();
    expect(dialog().getByText('Not on the hour.')).toBeInTheDocument();
  });

  it('shows any other failure as a message', async () => {
    atMondayMorning();
    const {user} = renderDialog(
      {patient: TEST_PATIENT},
      {
        'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
        'POST /api/appointments': () => json({}, 500),
      },
    );

    await screen.findByRole('dialog');
    await user.click(await dialog().findByRole('button', {name: '09:00'}));
    await user.click(dialog().getByRole('button', {name: 'Book'}));

    expect(await dialog().findByText('The appointment could not be saved. Please try again.')).toBeInTheDocument();
  });

  it('looks ahead for the first free day, and moves between weeks', async () => {
    // Friday 27 November at 17:50: nothing more this week.
    atMondayMorning('2026-11-27T17:50:00Z');
    const {user, fetch} = renderDialog({patient: TEST_PATIENT}, {
      'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
      'GET /api/calendar': (request) => {
        const from = new URL(request.url).searchParams.get('From')!;
        return json(testCalendar({from, dayCount: 5, today: '2026-11-27'}));
      },
    });

    await screen.findByRole('dialog');
    expect(await dialog().findByText('Week of Monday 30 November')).toBeInTheDocument();
    expect(await dialog().findByRole('button', {name: 'Mon 30, 39 free times'})).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET /api/calendar?StaffUserId=${STAFF_ME.userId}&From=2026-11-23&To=2026-11-27`);

    await user.click(dialog().getByRole('button', {name: 'Previous week'}));
    expect(await dialog().findByText(/Nothing is free this week for test-user/)).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Previous week'}))).toBe(true);
    expect(dialog().getByText(/Nothing is free this week for test-user/)).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Fri 27, Full'}))).toBe(true);
    expect(dialog().getByRole('button', {name: 'Mon 23, Past'})).toBeInTheDocument();

    await user.click(dialog().getByRole('button', {name: 'Next week'}));
    await user.click(await dialog().findByRole('button', {name: 'Tue 1, 39 free times'}));
    expect(dialog().getByRole('group', {name: 'Morning times on Tuesday 1 December'})).toBeInTheDocument();
  });

  it('moves between times with the arrow keys, as one tab stop', async () => {
    atMondayMorning();
    const {user} = renderDialog({patient: TEST_PATIENT}, {'GET /api/procedures/patient-procedures/search': () => json(pageOf([]))});

    await screen.findByRole('dialog');
    const morning = await dialog().findByRole('group', {name: /Morning times/});
    const first = within(morning).getByRole('button', {name: '09:00'});
    first.focus();
    await user.keyboard('{ArrowRight}');
    expect(within(morning).getByRole('button', {name: '09:15'})).toHaveFocus();
    expect(first).toHaveAttribute('tabindex', '-1');
  });

  it("shows someone else's free times once they're chosen", async () => {
    atMondayMorning();
    const {user, fetch} = renderDialog(
      {patient: TEST_PATIENT},
      {
        'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
        'GET /api/calendar': (request) =>
          calendarFor(request, new URL(request.url).searchParams.get('StaffUserId') === STAFF_OTHER.userId ? {blocks: [testBlock(MONDAY, '08:00', '12:00')]} : {}),
      },
    );

    await screen.findByRole('dialog');
    await user.click(await dialog().findByRole('button', {name: '09:00'}));
    await user.click(dialog().getByRole('combobox', {name: /With/}));
    await user.click(screen.getByRole('option', {name: 'b.staff'}));

    expect(await dialog().findByRole('button', {name: 'Mon 23, 23 free times'})).toBeInTheDocument();
    expect(dialog().queryByRole('group', {name: /Morning times/})).not.toBeInTheDocument();
    expect(dialog().getByText('Choose a time, or add it to Requires Appointment to book later.')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET /api/calendar?StaffUserId=${STAFF_OTHER.userId}&From=2026-11-23&To=2026-11-27`);
  });

  it('says when the calendar cannot be loaded', async () => {
    atMondayMorning();
    renderDialog({patient: TEST_PATIENT}, {
      'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
      'GET /api/calendar': () => json({}, 500),
    });

    await screen.findByRole('dialog');
    expect(await dialog().findByText('The calendar could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('closes on Cancel, and starts afresh each time it opens', async () => {
    atMondayMorning();
    mockApi({
      'GET /api/calendar/staff': () => json(STAFF),
      'GET /api/calendar': (request) => calendarFor(request),
      'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
    });
    function Harness() {
      const [isOpen, setIsOpen] = useState(false);
      return (
        <>
          <button type="button" onClick={() => setIsOpen(true)}>
            Open
          </button>
          <BookAppointmentDialog isOpen={isOpen} onClose={() => setIsOpen(false)} patient={TEST_PATIENT} />
        </>
      );
    }
    const {user} = renderWithApp(<Harness />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Open'}));
    await user.click(await dialog().findByRole('button', {name: '09:00'}));
    expect(dialog().getByText('Monday 23 November, 09:00 to 09:30 with test-user')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: 'Open'}));
    expect(await dialog().findByText('Choose a time, or add it to Requires Appointment to book later.')).toBeInTheDocument();
  });

  it('books with Ctrl+S once a time is chosen, and not before', async () => {
    atMondayMorning();
    const {user, fetch} = renderDialog(
      {patient: TEST_PATIENT},
      {
        'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
        'POST /api/appointments': () => json(testAppointment()),
      },
    );

    await screen.findByRole('dialog');
    const time = await dialog().findByRole('button', {name: '09:00'});
    time.focus();
    fireEvent.keyDown(time, {key: 's', ctrlKey: true});
    expect(calls(fetch).some((c) => c.startsWith('POST'))).toBe(false);
    await user.click(time);
    time.focus();
    fireEvent.keyDown(time, {key: 's', ctrlKey: true});
    await waitFor(() => expect(calls(fetch)).toContain('POST /api/appointments'));
  });

  it('shows a closed day in its week as closed, with nothing to choose', async () => {
    atMondayMorning();
    renderDialog(
      {patient: TEST_PATIENT},
      {
        'GET /api/procedures/patient-procedures/search': () => json(pageOf([])),
        'GET /api/calendar': (request) => {
          const from = new URL(request.url).searchParams.get('From')!;
          const calendar = testCalendar({from, dayCount: 5, today: MONDAY});
          calendar.days = calendar.days!.map((d) => (d.date === '2026-11-25' ? {...d, isWorkingDay: false, closedDayName: 'Staff training day'} : d));
          return json(calendar);
        },
      },
    );

    const closed = await screen.findByRole('button', {name: 'Wed 25, closed: Staff training day'});
    expect(closed).toHaveTextContent('Closed');
    expect(isDisabled(closed)).toBe(true);
    expect(dialog().getByRole('button', {name: 'Thu 26, 39 free times'})).toBeInTheDocument();
  });

  it("won't book a restricted patient, and says why", async () => {
    atMondayMorning();
    const {user, fetch} = renderDialog(
      {patient: {...TEST_PATIENT, isRestricted: true, restrictionReason: 'Synthetic accuracy check'}},
      {'GET /api/procedures/patient-procedures/search': () => json(pageOf([]))},
    );

    await screen.findByRole('dialog');
    const banner = await dialog().findByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('Restricted')).toBeInTheDocument();
    // Why, on screen: the restricted banner above shows only its label and summary.
    expect(dialog().getByText("This patient's record is restricted, so it can't be changed until an admin lifts the restriction.")).toBeInTheDocument();
    const details = await openAlertDetails(user, 'Restricted record', openDialog()!);
    expect(within(details).getByText(/Restricted record: .* an appointment cancelled/)).toBeInTheDocument();
    expect(within(details).getByText('Reason: Synthetic accuracy check')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Add to Requires Appointment'}))).toBe(true);
    await user.click(await dialog().findByRole('button', {name: '09:00'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Book'}))).toBe(true);
    fireEvent.keyDown(dialog().getByRole('button', {name: '09:00'}), {key: 's', ctrlKey: true});
    expect(calls(fetch).some((c) => c.startsWith('POST'))).toBe(false);
  });

  it("won't reschedule an appointment the API says can't be, and says so", async () => {
    atMondayMorning();
    const booked = testAppointment({id: 'appt-1', canReschedule: false});
    renderDialog({appointment: booked});

    await screen.findByRole('dialog');
    expect(await dialog().findByText('This appointment can’t be booked now.')).toBeInTheDocument();
    await chooseFirstTime();
    expect(isDisabled(dialog().getByRole('button', {name: 'Reschedule'}))).toBe(true);
  });

  it("says when the patient's details can't be read, naming them from the appointment", async () => {
    atMondayMorning();
    renderDialog({appointment: testRequested()}, {'GET /api/patients/patient-1': () => json({}, 500)});

    await screen.findByRole('dialog');
    expect(await dialog().findByText('Test Patient: their details could not be loaded.')).toBeInTheDocument();
  });
});

/** Picks the first free morning time in the open dialog. */
async function chooseFirstTime() {
  const morning = await dialog().findByRole('group', {name: /Morning times/});
  fireEvent.click(within(morning).getAllByRole('button')[0]);
}
