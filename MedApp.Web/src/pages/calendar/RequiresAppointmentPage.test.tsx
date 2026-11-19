import {screen, waitFor, within} from '@testing-library/react';
import {STAFF_ME, STAFF_OTHER, TEST_PATIENT, testAppointment, testCalendar, testRequested} from '../../test/calendar';
import {calls, json, managerUser, mockApi, openDialog, pageOf, renderWithApp, searchBar, staffUser} from '../../test/render';
import type {RequiresAppointmentSearch} from './calendarSearch';
import {DID_NOT_ATTEND_INTRO, missedWords, RequiresAppointmentPage, requiresAppointmentPageQuery, requiresAppointmentQuery} from './RequiresAppointmentPage';

const MONDAY = '2026-11-23';

function renderPage(search: RequiresAppointmentSearch = {}, handlers: Parameters<typeof mockApi>[0] = {}, user = staffUser) {
  const onSearchChange = vi.fn();
  const fetch = mockApi({
    'GET /api/appointments/search': () =>
      json(
        pageOf([
          testRequested({id: 'req-1', pathwayDueDate: '2026-11-20', isAutomatic: true}),
          testRequested({id: 'req-2', patientName: 'Alex Sample', pathwayId: null, pathwayTitle: null, stageName: null, pathwayDueDate: null, visitRequirement: null}),
        ]),
      ),
    'GET /api/calendar/staff': () => json([STAFF_ME, STAFF_OTHER]),
    'GET /api/calendar': (request) => json(testCalendar({from: new URL(request.url).searchParams.get('From')!, dayCount: 5, today: MONDAY})),
    'GET /api/pathways/patient-pathways/pathway-1': () => json({id: 'pathway-1', assignments: [{userId: STAFF_OTHER.userId, username: 'b.staff'}]}),
    'GET /api/patients/patient-1': () => json(TEST_PATIENT),
    ...handlers,
  });
  const result = renderWithApp(<RequiresAppointmentPage search={search} onSearchChange={onSearchChange} />, {user});
  return {...result, fetch, onSearchChange};
}

describe('RequiresAppointmentPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T07:00:00Z')});
  });

  it('asks the API for Requested appointments, soonest due first', () => {
    expect(requiresAppointmentQuery({}, 20)).toEqual({Statuses: ['Requested'], Page: 1, PageSize: 20, Sort: 'dueDate', Ascending: true, Search: undefined});
    expect(requiresAppointmentQuery({page: 2, sort: 'Patient', asc: false, patient: 'Sam'})).toMatchObject({Page: 2, Sort: 'patient', Ascending: false, Search: 'Sam'});
  });

  it('lists what requires an appointment: who, what for, the visit, when it is due, and who asked', async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('heading', {name: 'Requires Appointment', level: 1})).toBeInTheDocument();
    expect(await screen.findByText('2 appointments to book')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Test Patient'})).toHaveAttribute('href', '/patients/patient-1');
    expect(screen.getByRole('link', {name: 'Knee review'})).toHaveAttribute('href', '/pathways/pathway-1');
    expect(screen.getByText('Assessment')).toBeInTheDocument();
    expect(screen.getByText('Visit required')).toBeInTheDocument();
    expect(screen.getByText('Overdue')).toBeInTheDocument();
    expect(screen.getByText('Automatic')).toBeInTheDocument();
    expect(screen.getByText('Not for a pathway')).toBeInTheDocument();
    // No pathway, so no target date: a dash, as other lists show a missing value.
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.queryByText('None')).not.toBeInTheDocument();
    expect(calls(fetch)).toContain('GET /api/appointments/search?Statuses=Requested&Page=1&PageSize=10&Sort=dueDate&Ascending=true');
  });

  it("lists them for someone who can't book (a manager), with nothing to book them with", async () => {
    renderPage({}, {}, managerUser);

    expect(await screen.findByText('2 appointments to book')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Book'})).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', {name: 'Actions'})).not.toBeInTheDocument();
  });

  it("books one, suggesting the pathway's only assignee", async () => {
    const {user} = renderPage();

    const rows = await screen.findAllByRole('button', {name: 'Book'});
    await user.click(rows[0]);

    expect(await screen.findByRole('heading', {name: 'Book Appointment'})).toBeInTheDocument();
    const dialog = within(openDialog()!);
    await waitFor(() => expect(dialog.getByRole('combobox', {name: /With/})).toHaveTextContent('b.staff'));
    expect(await dialog.findByRole('region', {name: 'Patient: Patient, Test'})).toHaveTextContent('NHS No. 999 000 0018');
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it("doesn't offer to book a restricted patient's appointment", async () => {
    renderPage({}, {'GET /api/appointments/search': () => json(pageOf([testRequested({canBook: false}), testRequested({id: 'req-2'})]))});

    expect(await screen.findByText('Can’t be booked')).toBeInTheDocument();
    expect(screen.getAllByRole('button', {name: 'Book'})).toHaveLength(1);
  });

  it('sorts and pages through the URL', async () => {
    const {user, onSearchChange} = renderPage({}, {'GET /api/appointments/search': () => json(pageOf([testRequested()], 25))});

    await user.click(await screen.findByRole('button', {name: /Requested/}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined, sort: 'Requested', asc: true});
    await user.click(screen.getByRole('button', {name: 'Go to page 2'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 2});
  });

  it('says when nothing matches a search, and when nothing requires an appointment', async () => {
    const empty = {'GET /api/appointments/search': () => json(pageOf([]))};
    const {user, onSearchChange, unmount} = renderPage({patient: 'Nobody', sort: 'Patient'}, empty);

    expect(await screen.findByText('No appointments match your search')).toBeInTheDocument();
    expect(screen.getByText('0 matching appointments')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Clear Search'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Patient', asc: undefined});
    unmount();

    renderPage({}, empty);
    expect(await screen.findByText('Nothing requires an appointment')).toBeInTheDocument();
  });

  it('shows its patient search in the bar, and clears it', async () => {
    const {user, onSearchChange} = renderPage({patient: 'Sample', page: 2});

    await waitFor(() => expect(searchBar()).toHaveTextContent(/Patient: contains\s*Sample/));
    await user.click(within(searchBar()).getByRole('button', {name: /clear/i}));
    await waitFor(() => expect(onSearchChange).toHaveBeenLastCalledWith({patient: undefined, page: undefined}));
  });

  it('says when the list cannot be loaded', async () => {
    renderPage({}, {'GET /api/appointments/search': () => json({}, 500)});

    expect(await screen.findByText('The appointments could not be loaded. Please try again.')).toBeInTheDocument();
  });

  describe('Did not attend', () => {
    const missed = testAppointment({
      id: 'missed-1',
      date: '2026-11-16',
      status: 'DidNotAttend',
      canCancel: false,
      canReschedule: false,
      canEditNotes: true,
      canRecordDnaFollowUp: true,
      missedOnPathwayCount: 2,
    });
    const reviewed = testAppointment({
      id: 'missed-2',
      date: '2026-11-18',
      start: '14:00',
      end: '14:30',
      status: 'DidNotAttend',
      patientName: 'Alex Sample',
      pathwayId: null,
      pathwayTitle: null,
      stageName: null,
      canRecordDnaFollowUp: true,
      missedOnPathwayCount: 1,
      dnaFollowUp: 'ClinicianToReview',
      dnaFollowUpByUsername: 'b.staff',
      dnaFollowUpAtUtc: '2026-11-19T10:00:00Z',
    });
    const dnaList = {'GET /api/appointments/search': () => json(pageOf([missed, reviewed]))};

    it('asks the API for the Did not attend view, oldest first, keeping the page and patient search', () => {
      expect(requiresAppointmentPageQuery({view: 'didNotAttend', page: 2, patient: 'Sam'}, 20)).toEqual({
        View: 'didNotAttend',
        Sort: 'start',
        Ascending: true,
        Page: 2,
        PageSize: 20,
        Search: 'Sam',
      });
      expect(requiresAppointmentPageQuery({}, 20)).toEqual(requiresAppointmentQuery({}, 20));
      expect(missedWords({pathwayId: 'p', missedOnPathwayCount: 3})).toBe('3 missed on this pathway');
      expect(missedWords({pathwayId: null, missedOnPathwayCount: null})).toBe('1 missed (not on a pathway)');
    });

    it('lists missed appointments: who, when, what for, how many were missed there, and the follow-up so far', async () => {
      const {fetch} = renderPage({view: 'didNotAttend'}, dnaList);

      expect(await screen.findByText('2 missed appointments to follow up')).toBeInTheDocument();
      expect(screen.getByText(DID_NOT_ATTEND_INTRO)).toBeInTheDocument();
      expect(screen.getByText('Mon 16 Nov 2026, 09:00 to 09:30')).toBeInTheDocument();
      expect(screen.getByRole('link', {name: 'Knee review: Assessment'})).toHaveAttribute('href', '/pathways/pathway-1');
      expect(screen.getByText('2 missed on this pathway')).toBeInTheDocument();
      expect(screen.getByText('1 missed (not on a pathway)')).toBeInTheDocument();
      expect(screen.getByText('None yet')).toBeInTheDocument();
      expect(screen.getByText('Clinician to review, by b.staff, 19 Nov 2026, 10:00')).toBeInTheDocument();
      expect(calls(fetch)).toContain('GET /api/appointments/search?View=didNotAttend&Sort=start&Ascending=true&Page=1&PageSize=10');
    });

    it('records a follow-up from a row, showing who the patient is', async () => {
      const {user, fetch} = renderPage({view: 'didNotAttend'}, {
        ...dnaList,
        'POST /api/appointments/missed-1/dna-follow-up': () => json({...missed, dnaFollowUp: 'Rebook'}),
      });

      await user.click(await screen.findByRole('button', {name: 'Record Follow-up for Test Patient'}));
      const dialog = within(openDialog()!);
      expect(await dialog.findByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
      await user.click(dialog.getByRole('radio', {name: 'To be rebooked'}));
      await user.click(dialog.getByRole('button', {name: 'Record Follow-up'}));

      await waitFor(() => expect(openDialog()).toBeNull());
      const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
      expect(await request.clone().json()).toEqual({decision: 'Rebook', detail: null});
    });

    it('opens a missed appointment, to book another from it', async () => {
      const {user} = renderPage({view: 'didNotAttend'}, {...dnaList, 'GET /api/appointments/missed-1': () => json(missed)});

      await user.click(await screen.findByRole('button', {name: 'Open the appointment for Test Patient'}));
      expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
      expect(await within(openDialog()!).findByRole('button', {name: 'Book Another'})).toBeInTheDocument();
    });

    it("offers only Open to someone who can't book (a manager)", async () => {
      renderPage({view: 'didNotAttend'}, dnaList, managerUser);

      expect(await screen.findAllByRole('button', {name: /^Open the appointment/})).toHaveLength(2);
      expect(screen.queryByRole('button', {name: /^Record Follow-up/})).not.toBeInTheDocument();
    });

    it('switches between the lists through the URL, keeping the patient search', async () => {
      const {user, onSearchChange, unmount} = renderPage({patient: 'Sam', page: 3});

      await user.click(await screen.findByRole('radio', {name: 'Did not attend'}));
      expect(onSearchChange).toHaveBeenLastCalledWith({view: 'didNotAttend', patient: 'Sam'});
      unmount();

      const second = renderPage({view: 'didNotAttend'}, dnaList);
      await second.user.click(await screen.findByRole('radio', {name: 'To book'}));
      expect(second.onSearchChange).toHaveBeenLastCalledWith({view: undefined, patient: undefined});
    });

    it('says when nothing is waiting for a follow-up', async () => {
      renderPage({view: 'didNotAttend'}, {'GET /api/appointments/search': () => json(pageOf([]))});

      expect(await screen.findByText('No missed appointments waiting for a follow-up')).toBeInTheDocument();
    });
  });
});
