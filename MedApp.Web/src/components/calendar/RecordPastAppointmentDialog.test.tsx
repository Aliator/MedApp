import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {HalfViewContext} from '../pages/halfView';
import {calendarDay, STAFF_ME, STAFF_OTHER, TEST_PATIENT, testAppointment, testCalendar, ukTime} from '../../test/calendar';
import {calls, expectAnnounced, isDisabled, json, mockApi, openDialog, pageOf, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {pastStarts, RecordPastAppointmentDialog, type RecordPastAppointmentDialogProps} from './RecordPastAppointmentDialog';

/** Today in these tests: Wednesday 18 November 2026, 10:20 UK time (UTC in November). */
const TODAY = '2026-11-18';
const FROM = '2026-10-19';
/** Monday this week, closed (a staff training day), and so not offered. */
const CLOSED = '2026-11-16';
const STAFF = [STAFF_ME, STAFF_OTHER];

/** The last 31 days of a person's calendar: Monday closed, and the signed-in user with a booked appointment at 09:00 today. */
function lastMonth(request: Request) {
  const url = new URL(request.url);
  const staffId = url.searchParams.get('StaffUserId');
  const staff = STAFF.find((s) => s.userId === staffId) ?? STAFF_ME;
  const calendar = testCalendar({
    from: url.searchParams.get('From')!,
    dayCount: 31,
    today: TODAY,
    staff,
    appointments: staff === STAFF_ME ? [testAppointment({id: 'busy', date: TODAY, start: '09:00', end: '09:30'})] : [],
  });
  calendar.days = calendar.days!.map((d) => (d.date === CLOSED ? {...d, isWorkingDay: false, closedDayName: 'Staff training day'} : d));
  return json(calendar);
}

const recorded = testAppointment({id: 'walk-in', date: TODAY, start: '10:00', end: '10:30', status: 'Attended', isRecordedAfterEvent: true});

function renderDialog(props: Partial<RecordPastAppointmentDialogProps> = {}, handlers: Parameters<typeof mockApi>[0] = {}, isNarrow = false) {
  const onClose = vi.fn();
  const onSaved = vi.fn();
  const fetch = mockApi({
    'GET /api/calendar/staff': () => json(STAFF),
    'GET /api/calendar': lastMonth,
    'POST /api/appointments/past': () => json(recorded, 201),
    ...handlers,
  });
  const ui = (
    <SaveShortcutProvider>
      <RecordPastAppointmentDialog isOpen onClose={onClose} onSaved={onSaved} {...props} />
    </SaveShortcutProvider>
  );
  const result = renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{ui}</HalfViewContext.Provider> : ui, {user: staffUser});
  return {...result, fetch, onClose, onSaved};
}

const dialog = () => within(openDialog() ?? document.body);
const bodyOf = async (fetch: ReturnType<typeof mockApi>) => {
  const request = fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'POST').at(-1)!;
  return request.clone().json();
};

async function choose(user: ReturnType<typeof renderDialog>['user'], field: RegExp, option: string | RegExp) {
  await user.click(dialog().getByRole('combobox', {name: field}));
  await user.click(await screen.findByRole('option', {name: option}));
}

const FOR_STAGE = {
  patient: TEST_PATIENT,
  procedure: {id: 'procedure-1', title: 'Knee review'},
  stage: {id: 'stage-1', name: 'Assessment'},
};

// Slow under coverage on a loaded machine (CI): give it room.
describe('RecordPastAppointmentDialog', {timeout: 30_000}, () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-18T10:20:00Z')});
  });

  it("records a stage's past visit: who it was, a working day in the last 30, a time that had started, and the outcome", async () => {
    withAnnouncements();
    const {user, fetch, onSaved, onClose} = renderDialog(FOR_STAGE);

    expect(await screen.findByRole('heading', {name: 'Record Past Visit'})).toBeInTheDocument();
    // Who it's for, as the patient banner shows them, so staff can check they have the right patient.
    const banner = await dialog().findByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('NHS No. 999 000 0018')).toBeInTheDocument();
    expect(dialog().getByText(/already happened: a walk-in, or one booked outside MedApp\. It.s recorded with your\s+name \(test-user\)/)).toBeInTheDocument();
    expect(dialog().getByText('Knee review: Assessment')).toBeInTheDocument();
    expect(dialog().queryByRole('combobox', {name: /Patient/})).not.toBeInTheDocument();
    expect(dialog().getByText(/waiting in Needs booking, this records that visit/)).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET /api/calendar?StaffUserId=${STAFF_ME.userId}&From=${FROM}&To=${TODAY}`);

    // Today first; then the working days back to 30 days ago, never a weekend or the closed day.
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Day/})).toHaveTextContent('Today, Wednesday 18 November'));
    await user.click(dialog().getByRole('combobox', {name: /Day/}));
    const days = screen.getAllByRole('option').map((o) => o.textContent);
    expect(days[0]).toBe('Today, Wednesday 18 November');
    expect(days).toContain('Tuesday 17 November');
    expect(days).not.toContain('Monday 16 November');
    expect(days).not.toContain('Sunday 15 November');
    expect(days.at(-1)).toBe('Monday 19 October');
    await user.click(screen.getByRole('option', {name: /^Today/}));

    // Only times that had started by 10:20, and none over the 09:00 appointment.
    await user.click(dialog().getByRole('combobox', {name: /Start/}));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['08:00', '08:15', '08:30', '09:30', '09:45', '10:00', '10:15']);
    await user.click(screen.getByRole('option', {name: '10:00'}));

    // Nothing is chosen as the outcome until someone chooses.
    expect(dialog().getByRole('radio', {name: 'Attended'})).not.toBeChecked();
    expect(dialog().getByRole('radio', {name: 'Did not attend'})).not.toBeChecked();
    expect(isDisabled(dialog().getByRole('button', {name: 'Record'}))).toBe(true);
    await user.click(dialog().getByRole('radio', {name: 'Attended'}));
    expect(dialog().getByText('Wednesday 18 November, 10:00 to 10:30 with test-user, Attended')).toBeInTheDocument();
    await user.type(dialog().getByRole('textbox', {name: /Notes/}), ' Came in without an appointment ');
    await user.click(dialog().getByRole('button', {name: 'Record'}));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(recorded));
    expect(await bodyOf(fetch)).toEqual({
      patientId: 'patient-1',
      procedureId: 'procedure-1',
      stageId: 'stage-1',
      staffUserId: STAFF_ME.userId,
      startUtc: ukTime(TODAY, '10:00').replace('Z', '.000Z'),
      endUtc: ukTime(TODAY, '10:30').replace('Z', '.000Z'),
      outcome: 'Attended',
      notes: 'Came in without an appointment',
    });
    expect(onClose).toHaveBeenCalled();
    await expectAnnounced('Past appointment recorded');
  });

  it("finds the patient by name, then what it was for, with the calendar's owner and another day and length", async () => {
    const {user, fetch} = renderDialog(
      {staffUserId: STAFF_OTHER.userId},
      {
        'GET /api/patients/search': () => json(pageOf([{id: 'patient-9', firstName: 'Alex', lastName: 'Sample', dateOfBirth: '1980-03-14', nhsNumber: '9990000026'}])),
        'GET /api/procedures/patient-procedures/search': () => json(pageOf([{id: 'procedure-9', title: 'Hip review', dueDate: '2026-12-10', currentStageName: 'Consent'}])),
        'GET /api/procedures/patient-procedures/procedure-9': () =>
          json({
            id: 'procedure-9',
            stages: [
              {id: 'stage-a', name: 'Referral', state: 'Completed'},
              {id: 'stage-b', name: 'Consent', state: 'InProgress', isCurrent: true, visitRequirement: 'Required'},
            ],
          }),
      },
    );

    expect(await screen.findByRole('heading', {name: 'Record Past Appointment'})).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /With/})).toHaveTextContent('b.staff'));
    await user.type(dialog().getByRole('combobox', {name: /Patient/}), 'al');
    await user.click(await screen.findByRole('option', {name: /Alex Sample/}));
    expect(await dialog().findByRole('region', {name: 'Patient: Sample, Alex'})).toHaveTextContent('NHS No. 999 000 0026');

    await choose(user, /For/, /Hip review/);
    await user.click(await dialog().findByRole('combobox', {name: /Stage/}));
    expect(screen.queryByRole('option', {name: /Referral/})).not.toBeInTheDocument();
    expect(screen.getByRole('option', {name: /Consent/})).toHaveTextContent('Current stage · Visit required');
    await user.click(screen.getByRole('option', {name: /Consent/}));
    expect(await screen.findByRole('heading', {name: 'Record Past Appointment'})).toBeInTheDocument();

    // b.staff has nothing booked today, so 09:00 is free for them; an hour long, it still has to have started.
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Day/})).toHaveTextContent('Today'));
    await choose(user, /Length/, '1 hour');
    await choose(user, /Day/, 'Tuesday 17 November');
    await choose(user, /Start/, '17:00');
    await user.click(dialog().getByRole('radio', {name: 'Did not attend'}));
    // Ctrl+S records it, as Record would.
    fireEvent.keyDown(dialog().getByRole('textbox', {name: /Notes/}), {key: 's', ctrlKey: true});

    await waitFor(async () =>
      expect(await bodyOf(fetch)).toMatchObject({
        patientId: 'patient-9',
        procedureId: 'procedure-9',
        stageId: 'stage-b',
        staffUserId: STAFF_OTHER.userId,
        startUtc: ukTime('2026-11-17', '17:00').replace('Z', '.000Z'),
        endUtc: ukTime('2026-11-17', '18:00').replace('Z', '.000Z'),
        outcome: 'DidNotAttend',
        notes: null,
      }),
    );
    expect(calls(fetch)).toContain(`GET /api/calendar?StaffUserId=${STAFF_OTHER.userId}&From=${FROM}&To=${TODAY}`);
  });

  it("shows the API's field messages, and keeps a 409 open with its message, refreshing the calendars", async () => {
    let attempt = 0;
    const {user, fetch, onClose} = renderDialog(FOR_STAGE, {
      'POST /api/appointments/past': () =>
        ++attempt === 1
          ? json({title: 'Validation failed', status: 400, errors: {StartUtc: ['That time had not started yet.'], Outcome: ['Choose an outcome.']}}, 400)
          : json({title: "That overlaps test-user's appointment with Test Patient, 10:00 to 10:30.", status: 409}, 409),
    });

    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Day/})).toHaveTextContent('Today'));
    await choose(user, /Start/, '10:00');
    await user.click(dialog().getByRole('radio', {name: 'Attended'}));
    await user.click(dialog().getByRole('button', {name: 'Record'}));

    expect((await dialog().findAllByText('That time had not started yet.')).length).toBeGreaterThan(0);
    expect(dialog().getAllByText('Choose an outcome.').length).toBeGreaterThan(0);
    const calendarReads = () => calls(fetch).filter((c) => c.startsWith('GET /api/calendar?')).length;
    const before = calendarReads();
    await user.click(dialog().getByRole('button', {name: 'Record'}));

    expect(await dialog().findByText("That overlaps test-user's appointment with Test Patient, 10:00 to 10:30.")).toBeInTheDocument();
    await waitFor(() => expect(calendarReads()).toBeGreaterThan(before));
    expect(onClose).not.toHaveBeenCalled();
  });

  it("won't record for a restricted patient", async () => {
    const {user, fetch} = renderDialog({...FOR_STAGE, patient: {...TEST_PATIENT, isRestricted: true, restrictionReason: 'Synthetic accuracy check'}});

    const banner = await dialog().findByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('Restricted')).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Day/})).toHaveTextContent('Today'));
    await choose(user, /Start/, '10:00');
    await user.click(dialog().getByRole('radio', {name: 'Attended'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Record'}))).toBe(true);
    fireEvent.keyDown(dialog().getByRole('textbox', {name: /Notes/}), {key: 's', ctrlKey: true});
    expect(calls(fetch).some((c) => c.startsWith('POST'))).toBe(false);
  });

  it("says when the calendar can't be loaded, and lays out in one column in a half view", async () => {
    renderDialog(FOR_STAGE, {'GET /api/calendar': () => json({title: 'Down'}, 500)}, true);

    expect(await dialog().findByText('The calendar could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('starts afresh each time it opens', async () => {
    mockApi({'GET /api/calendar/staff': () => json(STAFF), 'GET /api/calendar': lastMonth});
    function Toggled() {
      const [isOpen, setIsOpen] = useState(true);
      return (
        <>
          <button type="button" onClick={() => setIsOpen((open) => !open)}>
            Toggle
          </button>
          <RecordPastAppointmentDialog isOpen={isOpen} onClose={() => setIsOpen(false)} {...FOR_STAGE} />
        </>
      );
    }
    const {user} = renderWithApp(<Toggled />, {user: staffUser});

    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Day/})).toHaveTextContent('Today'));
    await user.click(dialog().getByRole('radio', {name: 'Attended'}));
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    fireEvent.click(screen.getByRole('button', {name: 'Toggle'}));

    await waitFor(() => expect(dialog().getByRole('radio', {name: 'Attended'})).not.toBeChecked());
  });
});

describe('pastStarts', () => {
  const day = calendarDay(TODAY, TODAY);
  const now = new Date('2026-11-18T10:20:00Z');

  it('offers the times that had started, inside the working day, in steps, around what is taken', () => {
    expect(pastStarts(day, [], 30, new Date('2026-11-18T08:20:00Z')).map((s) => s.slice(11, 16))).toEqual(['08:00', '08:15']);
    expect(pastStarts(day, [], 30, now, 5).length).toBe(29);
    // Late on a past day, the last start still ends by 18:00.
    expect(pastStarts(calendarDay('2026-11-17', TODAY), [], 60, now).at(-1)).toBe(new Date(ukTime('2026-11-17', '17:00')).toISOString());
  });

  it('offers nothing on a day that is not a working day, or before the day starts', () => {
    expect(pastStarts({...day, isWorkingDay: false, closedDayName: 'Staff training day'}, [], 30, now)).toEqual([]);
    expect(pastStarts({...day, workStartUtc: undefined}, [], 30, now)).toEqual([]);
    expect(pastStarts(day, [], 0, now)).toEqual([]);
    expect(pastStarts(day, [], 30, new Date('2026-11-18T07:59:00Z'))).toEqual([]);
  });
});
