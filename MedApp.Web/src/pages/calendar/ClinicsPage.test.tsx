import {screen, waitFor, within} from '@testing-library/react';
import {QueryClient} from '@tanstack/react-query';
import {useState} from 'react';
import {testSession, testSessionDetail} from '../../test/clinics';
import {adminUser, calls, json, managerUser, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {clinicsWeek, ClinicsPage, preloadClinicsPage, type ClinicsPageProps} from './ClinicsPage';
import type {ClinicsSearch} from './calendarSearch';

const SESSIONS = '/api/clinics/sessions';
const SITES = [{id: 'site-main', name: 'Main site', rooms: []}];

function renderPage(props: Partial<ClinicsPageProps> = {}, sessions = [testSession({booked: 1})], user = managerUser, handlers: Parameters<typeof mockApi>[0] = {}) {
  const onSearchChange = vi.fn();
  const fetch = mockApi({[`GET ${SESSIONS}`]: () => json(sessions), 'GET /api/sites': () => json(SITES), ...handlers});
  const result = renderWithApp(<ClinicsPage search={{}} onSearchChange={onSearchChange} {...props} />, {path: '/calendar/clinics', user});
  return {...result, fetch, onSearchChange};
}

describe('ClinicsPage', {timeout: 30_000}, () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T10:00:00Z')});
  });

  it("lists the week's sessions, each with its room, clinician, team and slots", async () => {
    const {fetch} = renderPage({}, [testSession({booked: 1}), testSession({id: 'session-2', status: 'Cancelled', freeSlotCount: 0, freeSlotStartsUtc: []})]);

    expect(await screen.findByRole('heading', {name: 'Clinics', level: 1})).toBeInTheDocument();
    expect(await screen.findByText('Week of Monday 23 November · 1 session')).toBeInTheDocument();
    const row = (await screen.findAllByText('Synthetic dermatology'))[0].closest('tr')!;
    expect(within(row).getByText('Tue 24')).toBeInTheDocument();
    expect(within(row).getByText('09:00 to 12:00')).toBeInTheDocument();
    expect(within(row).getByText('Room 2')).toBeInTheDocument();
    expect(within(row).getByText('20 min · 8 of 9 free')).toBeInTheDocument();
    expect(screen.getByText('Cancelled')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${SESSIONS}?From=2026-11-23&To=2026-11-29&IncludeCancelled=true`);
    expect(screen.getByRole('link', {name: 'Clinic Patterns'})).toHaveAttribute('href', '/calendar/clinics/patterns');
    expect(screen.getByRole('button', {name: 'New Session'})).toBeInTheDocument();
  });

  it('moves between weeks and sites through the address, and opens a session', async () => {
    const {user, onSearchChange} = renderPage({search: {date: '2026-11-25'}});

    await user.click(await screen.findByRole('button', {name: 'Next week'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-11-30'});
    await user.click(screen.getByRole('button', {name: 'Previous week'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-11-16'});
    await user.click(screen.getByRole('combobox', {name: /Site/}));
    await user.click(await screen.findByRole('option', {name: 'Main site'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-11-25', site: 'site-main'});
    await user.click(screen.getByRole('button', {name: /^Open the Synthetic dermatology session/}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-11-25', session: 'session-1'});
  });

  it('opens the session in the address in its dialog', async () => {
    renderPage({search: {session: 'session-1'}}, undefined, managerUser, {[`GET ${SESSIONS}/session-1`]: () => json(testSessionDetail())});
    expect(await screen.findByRole('heading', {name: 'Clinic Session'})).toBeInTheDocument();
  });

  it('offers no changes without ManageClinics, and says when the week is empty', async () => {
    renderPage({}, [], staffUser);
    expect(await screen.findByText('No clinic sessions this week')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'New Session'})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Clinic Patterns'})).not.toBeInTheDocument();
  });

  it('works out the week from any day in it', () => {
    expect(clinicsWeek({date: '2026-11-29'})).toEqual({from: '2026-11-23', to: '2026-11-29'});
    expect(clinicsWeek({}, new Date('2026-11-25T10:00:00Z'))).toEqual({from: '2026-11-23', to: '2026-11-29'});
  });

  it('opens New Session, and from a session its form, each closing again', async () => {
    mockApi({
      [`GET ${SESSIONS}`]: () => json([testSession({booked: 1})]),
      [`GET ${SESSIONS}/session-1`]: () => json(testSessionDetail()),
      'GET /api/sites': () => json(SITES),
      'GET /api/calendar/staff': () => json([]),
      'GET /api/rooms': () => json([]),
      'GET /api/teams': () => json([]),
    });
    function Harness() {
      const [search, setSearch] = useState<ClinicsSearch>({});
      return <ClinicsPage search={search} onSearchChange={setSearch} />;
    }
    const {user} = renderWithApp(<Harness />, {path: '/calendar/clinics', user: managerUser});
    const dialog = () => within(openDialog()!);

    await user.click(await screen.findByRole('button', {name: 'New Session'}));
    expect(await screen.findByRole('heading', {name: 'New Clinic Session'})).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: /^Open the/}));
    await user.click(await within(document.body).findByRole('button', {name: 'Edit'}));
    expect(await screen.findByRole('heading', {name: 'Edit Clinic Session'})).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: /^Open the/}));
    await within(document.body).findByRole('button', {name: 'Edit'});
    await user.click(within(openDialog()!).getAllByRole('button', {name: 'Close'}).at(-1)!);
    await waitFor(() => expect(openDialog()).toBeNull());
    await user.click(screen.getByRole('button', {name: 'This week'}));
  });

  it('books a free slot from a session, for someone who can book', async () => {
    mockApi({
      [`GET ${SESSIONS}`]: () => json([testSession({booked: 1})]),
      [`GET ${SESSIONS}/session-1`]: () => json(testSessionDetail()),
      'GET /api/sites': () => json(SITES),
      'GET /api/calendar/staff': () => json([]),
    });
    function Harness() {
      const [search, setSearch] = useState<ClinicsSearch>({session: 'session-1'});
      return <ClinicsPage search={search} onSearchChange={setSearch} />;
    }
    const {user} = renderWithApp(<Harness />, {path: '/calendar/clinics', user: adminUser});
    await user.click(await within(document.body).findByRole('button', {name: 'Book 09:20'}));
    expect(await screen.findByRole('heading', {name: 'Book Appointment'})).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it("preloads the week's sessions and the sites", async () => {
    mockApi({[`GET ${SESSIONS}`]: () => json([]), 'GET /api/sites': () => json(SITES)});
    const queryClient = new QueryClient();
    const spy = vi.spyOn(queryClient, 'prefetchQuery');
    await preloadClinicsPage(queryClient, {});
    expect(spy).toHaveBeenCalledTimes(2);
  });
});
