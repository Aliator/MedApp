import {screen, waitFor, within} from '@testing-library/react';
import {STAFF_ME, STAFF_OTHER, TEST_PATIENT, testCalendar, testRequested} from '../../test/calendar';
import {calls, json, mockApi, openDialog, pageOf, renderWithApp, searchBar, staffUser} from '../../test/render';
import type {RequiresAppointmentSearch} from './calendarSearch';
import {RequiresAppointmentPage, requiresAppointmentQuery} from './RequiresAppointmentPage';

const MONDAY = '2026-11-23';

function renderPage(search: RequiresAppointmentSearch = {}, handlers: Parameters<typeof mockApi>[0] = {}) {
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
  const result = renderWithApp(<RequiresAppointmentPage search={search} onSearchChange={onSearchChange} />, {user: staffUser});
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
    expect(screen.getByText('Appointment')).toBeInTheDocument();
    // No pathway, so no target date: a dash, as other lists show a missing value.
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.queryByText('None')).not.toBeInTheDocument();
    expect(calls(fetch)).toContain('GET /api/appointments/search?Statuses=Requested&Page=1&PageSize=10&Sort=dueDate&Ascending=true');
  });

  it("books one, suggesting the pathway's only assignee", async () => {
    const {user} = renderPage();

    const rows = await screen.findAllByRole('button', {name: 'Book'});
    await user.click(rows[0]);

    expect(await screen.findByRole('heading', {name: 'Book Visit'})).toBeInTheDocument();
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
});
