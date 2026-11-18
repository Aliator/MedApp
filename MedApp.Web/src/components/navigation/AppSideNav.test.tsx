import {act, fireEvent, screen, waitFor, within} from '@testing-library/react';
import {HalfViewsContext} from '../pages/halfView';
import {NotepadProvider, useNotepad} from '../notepad/NotepadContext';
import {adminUser, calls, isDisabled, json, mockApi, mockFetch, openDialog, renderWithApp, rowMenuItem, staffUser} from '../../test/render';
import {AppSideNav, HALF_VIEW_SHADE, isCurrentSection, openPatientId, openPathwayId} from './AppSideNav';
import {SettingsSync} from '../../app/SettingsSync';
import {PINS_KEY} from './pins';

/** The IDs a labels lookup asked for. */
const askedIds = (request: Request) => new URL(request.url).searchParams.getAll('ids');

/** Answers the patients' labels lookup from `names` (by ID); IDs it doesn't have are left out, as the API does. */
const patientLabels = (names: Record<string, string>) => (request: Request) =>
  json(askedIds(request).filter((id) => names[id]).map((id) => ({id, name: names[id]})));

/** Answers the pathways' labels lookup from `labels` (by ID), leaving out IDs it doesn't have. */
const pathwayLabels = (labels: Record<string, {title: string; patientName: string}>) => (request: Request) =>
  json(askedIds(request).filter((id) => labels[id]).map((id) => ({id, ...labels[id]})));

/** The pinned IDs of one kind, as the account's settings keep them. */
const pinnedIds = (kind: 'patients' | 'pathways') =>
  ((JSON.parse(localStorage.getItem(PINS_KEY) ?? '{}') as Record<string, string[]>)[kind] ?? []);

describe('isCurrentSection', () => {
  it.each([
    ['/', '/', true],
    ['/patients', '/', false],
    ['/patients', '/patients', true],
    ['/patients/add', '/patients', true],
    ['/patients-archive', '/patients', false],
    ['/admin/users', '/admin/users', true],
  ])('pathname %s is under %s: %s', (pathname, path, expected) => {
    expect(isCurrentSection(pathname, path)).toBe(expected);
  });
});

describe('AppSideNav', () => {
  it('shows the admin pages and role to an admin', async () => {
    renderWithApp(<AppSideNav />, {user: adminUser});

    expect(await screen.findByRole('link', {name: /users/i})).toHaveAttribute('href', '/admin/users');
    expect(screen.getByRole('link', {name: 'Specialties'})).toHaveAttribute('href', '/admin/specialties');
    expect(screen.getByRole('link', {name: 'Stage Library'})).toHaveAttribute('href', '/admin/stages');
    expect(screen.getByRole('link', {name: 'Pathway Templates'})).toHaveAttribute('href', '/admin/pathway-templates');
    // Calendar templates are each person's own, reached from the Calendar page.
    expect(screen.queryByRole('link', {name: 'Calendar Templates'})).not.toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Activity Types'})).toHaveAttribute('href', '/admin/activity-types');
    expect(screen.getByRole('link', {name: 'Closed Days'})).toHaveAttribute('href', '/admin/closed-days');
    expect(screen.getByRole('link', {name: /audit log/i})).toHaveAttribute('href', '/admin/audit');
    expect(screen.getByRole('link', {name: 'Insights'})).toHaveAttribute('href', '/admin/insights');
    expect(screen.getByRole('link', {name: 'Monitoring'})).toHaveAttribute('href', '/admin/monitoring');
    expect(screen.getByRole('link', {name: /activity log/i})).toHaveAttribute('href', '/admin/activity');
    expect(screen.getByRole('link', {name: /retention review/i})).toHaveAttribute('href', '/admin/retention');
    expect(screen.getByRole('link', {name: 'Admin Settings'})).toHaveAttribute('href', '/admin/settings');
    expect(screen.getByText('test-admin')).toBeInTheDocument();
    expect(screen.getByText('Administrator')).toBeInTheDocument();
  });

  it('hides the admin pages from other users', async () => {
    renderWithApp(<AppSideNav />, {user: staffUser});

    expect(await screen.findByRole('link', {name: /patients/i})).toHaveAttribute('href', '/patients');
    expect(screen.queryByRole('link', {name: /^users$/i})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /audit log/i})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /activity log/i})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /retention review/i})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Admin Settings'})).not.toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
  });

  it('marks the current section', async () => {
    renderWithApp(<AppSideNav />, {user: adminUser, path: '/patients/add'});

    expect(await screen.findByRole('link', {name: /patients/i})).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', {name: 'My Worklist'})).not.toHaveAttribute('aria-current');
    // Home is the MedApp heading's, not a nav item.
    expect(screen.getByRole('link', {name: 'MedApp'})).toHaveAttribute('href', '/');
  });

  it('has the calendar for everyone, marked current on every page under it, Requires Appointment with no item of its own', async () => {
    const {unmount} = renderWithApp(<AppSideNav />, {user: staffUser, path: '/calendar'});
    expect(await screen.findByRole('link', {name: 'Calendar'})).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', {name: 'Calendar'})).toHaveAttribute('href', '/calendar');
    unmount();

    renderWithApp(<AppSideNav />, {user: staffUser, path: '/calendar/requires-appointment'});
    expect(await screen.findByRole('link', {name: 'Calendar'})).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('link', {name: /requires appointment|needs booking/i})).not.toBeInTheDocument();
  });

  it('has Referrals between My Worklist and Patients, with how many await triage, heard after its name', async () => {
    mockApi({'GET /api/referrals/awaiting-triage/count': () => json({count: 7, inYourSpecialties: true})});
    renderWithApp(<AppSideNav />, {user: staffUser, path: '/referrals/new'});

    const referrals = await screen.findByRole('link', {name: 'Referrals, 7 awaiting triage'});
    expect(referrals).toHaveAttribute('href', '/referrals');
    expect(referrals).toHaveAttribute('aria-current', 'page');
    const names = screen.getAllByRole('link').map((link) => link.getAttribute('href'));
    expect(names.indexOf('/referrals')).toBe(names.indexOf('/tasks') + 1);
    expect(names.indexOf('/patients')).toBe(names.indexOf('/referrals') + 1);
  });

  it('shows no count when nothing awaits triage, and 99+ for more than 99', async () => {
    mockApi({'GET /api/referrals/awaiting-triage/count': () => json({count: 0, inYourSpecialties: false})});
    const {unmount} = renderWithApp(<AppSideNav />, {user: staffUser});
    expect(await screen.findByRole('link', {name: 'Referrals'})).toBeInTheDocument();
    unmount();

    mockApi({'GET /api/referrals/awaiting-triage/count': () => json({count: 120, inYourSpecialties: false})});
    renderWithApp(<AppSideNav />, {user: staffUser});
    expect(await screen.findByRole('link', {name: 'Referrals, 120 awaiting triage'})).toHaveTextContent('99+');
  });

  it('navigates on the client when a link is clicked', async () => {
    const {user, router} = renderWithApp(<AppSideNav />, {user: adminUser});

    await user.click(await screen.findByRole('link', {name: /patients/i}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });

  it('signs out, clears cached data and goes to the login page', async () => {
    const fetch = mockFetch(() => new Response(null, {status: 204}));
    const {user, router, queryClient} = renderWithApp(<AppSideNav />, {user: adminUser});
    queryClient.setQueryData(['patients'], [{id: 'synthetic-patient'}]);

    // The requests to sign out (the bell asks for the notifications as well).
    const logouts = () => fetch.mock.calls.filter(([request]) => new URL((request as Request).url).pathname === '/api/auth/logout');

    await user.click(await screen.findByRole('button', {name: /logout/i}));
    // It asks first.
    expect(logouts()).toHaveLength(0);
    await user.click(within(openDialog()!).getByRole('button', {name: 'Log Out'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(logouts()).toHaveLength(1);
    await waitFor(() => expect(queryClient.getQueryData(['patients'])).toBeUndefined());
  });

  it('saves a pin made just before signing out, before signing out', async () => {
    const fetch = mockApi({
      'GET /api/auth/me/settings': () => json({settings: '{}', updatedAtUtc: null}),
      'PUT /api/auth/me/settings': () => new Response(null, {status: 204}),
      'POST /api/auth/logout': () => new Response(null, {status: 204}),
      'GET /api/patients/labels': patientLabels({[patientId]: 'Freya Ahmed'}),
    });
    const {user, router} = renderWithApp(
      <>
        <SettingsSync />
        <AppSideNav />
      </>,
      {user: staffUser, path: `/patients/${patientId}`},
    );
    await waitFor(() => expect(calls(fetch)).toContain('GET /api/auth/me/settings'));
    await screen.findByRole('link', {name: 'Freya Ahmed'});

    await user.click(await rowMenuItem(user, 'Freya Ahmed', 'Pin'));
    await user.click(await screen.findByRole('button', {name: /logout/i}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Log Out'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    const order = calls(fetch).filter((c) => c === 'PUT /api/auth/me/settings' || c === 'POST /api/auth/logout');
    expect(order).toEqual(['PUT /api/auth/me/settings', 'POST /api/auth/logout']);
    const put = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    const saved = JSON.parse(((await put.json()) as {settings: string}).settings) as Record<string, string>;
    expect(JSON.parse(saved[PINS_KEY])).toEqual({patients: [patientId], pathways: []});
  });

  it("clears the user's storage at sign-out, keeping only how the app looks", async () => {
    const patient = 'a0000000-0000-4000-8000-000000000001';
    localStorage.setItem('medapp.favourites', JSON.stringify({patients: {saved: [{id: 'f1', name: 'Synthetic', values: {last: 'Synthetic'}}]}}));
    localStorage.setItem(PINS_KEY, JSON.stringify({patients: [patient]}));
    localStorage.setItem('medapp.pinnedPatients.test-admin', JSON.stringify([patient]));
    localStorage.setItem('medapp.colorMode', 'dark');
    localStorage.setItem('medapp.textSize', 'large');
    localStorage.setItem('other.app', 'untouched');
    sessionStorage.setItem('medapp.halfView', `/patients/${patient}`);
    mockFetch(() => new Response(null, {status: 204}));
    const {user, router} = renderWithApp(<AppSideNav />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: /logout/i}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Log Out'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    await waitFor(() => expect(localStorage.getItem('medapp.favourites')).toBeNull());
    expect(localStorage.getItem(PINS_KEY)).toBeNull();
    expect(localStorage.getItem('medapp.pinnedPatients.test-admin')).toBeNull();
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
    expect(localStorage.getItem('medapp.colorMode')).toBe('dark');
    expect(localStorage.getItem('medapp.textSize')).toBe('large');
    expect(localStorage.getItem('other.app')).toBe('untouched');
    sessionStorage.clear();
  });

  it('has no light/dark mode button (it moved to Account Settings)', async () => {
    renderWithApp(<AppSideNav />, {user: adminUser});

    await screen.findByRole('link', {name: /users/i});
    expect(screen.queryByRole('button', {name: /switch to (light|dark) mode/i})).not.toBeInTheDocument();
  });
});

const patientId = 'f184a7b8-2585-4437-bcd4-e1700be38287';

describe('openPatientId', () => {
  it.each([
    [`/patients/${patientId}`, patientId],
    [`/patients/${patientId}/edit`, patientId],
    [`/patients/${patientId}/pathways`, patientId],
    ['/patients', null],
    ['/patients/add', null],
    ['/pathways', null],
  ])('pathname %s has open patient %s', (pathname, expected) => {
    expect(openPatientId(pathname)).toBe(expected);
  });
});

describe('AppSideNav with a patient open', () => {
  function renderAt(path: string) {
    mockApi({'GET /api/patients/labels': patientLabels({[patientId]: 'Freya Ahmed'})});
    return renderWithApp(<AppSideNav />, {user: staffUser, path});
  }

  it("shows the patient under Patients, marked as the current page, on any of their pages", async () => {
    renderAt(`/patients/${patientId}/edit`);

    const patient = await screen.findByRole('link', {name: 'Freya Ahmed'});
    expect(patient).toHaveAttribute('href', `/patients/${patientId}`);
    expect(patient).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', {name: /^patients$/i})).toHaveAttribute('href', '/patients');
    expect(screen.getByRole('link', {name: /^patients$/i})).not.toHaveAttribute('aria-current');
    // Patients has a menu (with its show and hide), its button hidden until the row is hovered.
    expect(document.querySelector('button[aria-label="Patients options"]')).not.toBeNull();
  });

  it('shows no patient on the patients list', async () => {
    renderAt('/patients');

    expect(await screen.findByRole('link', {name: /^patients$/i})).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('link', {name: 'Freya Ahmed'})).not.toBeInTheDocument();
  });
});

describe('AppSideNav pinned patients', () => {
  const patients: Record<string, [string, string]> = {
    [patientId]: ['Freya', 'Ahmed'],
    'a0000000-0000-4000-8000-000000000002': ['Priya', 'Ali'],
  };

  function renderAt(path: string, pinned: string[] = []) {
    if (pinned.length) localStorage.setItem(PINS_KEY, JSON.stringify({patients: pinned}));
    const fetch = mockApi({
      'GET /api/patients/labels': patientLabels(Object.fromEntries(Object.entries(patients).map(([id, [first, last]]) => [id, `${first} ${last}`]))),
    });
    return {fetch, ...renderWithApp(<AppSideNav />, {user: staffUser, path})};
  }

  it("pins the open patient, from the row's menu (its button shown while the row is hovered)", async () => {
    const {user} = renderAt(`/patients/${patientId}`);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    expect(screen.queryByRole('button', {name: 'Freya Ahmed options'})).not.toBeInTheDocument();
    await user.click(await rowMenuItem(user, 'Freya Ahmed', 'Pin'));

    expect(pinnedIds('patients')).toEqual([patientId]);
    await user.unhover(screen.getByRole('link', {name: 'Freya Ahmed'}));
    expect(await rowMenuItem(user, 'Freya Ahmed', 'Unpin')).toBeInTheDocument();
  });

  it("folds the patients under Patients away from its row's menu", async () => {
    const {user} = renderAt('/pathways', [patientId]);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    await user.click(await rowMenuItem(user, 'Patients', 'Hide pinned and open patients'));
    expect(screen.queryByRole('link', {name: 'Freya Ahmed'})).not.toBeInTheDocument();

    await user.unhover(screen.getByRole('link', {name: 'Patients'}));
    await user.click(await rowMenuItem(user, 'Patients', 'Show pinned and open patients'));
    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toBeInTheDocument();
  });

  it('keeps pinned patients under Patients on other pages', async () => {
    renderAt('/pathways', [patientId, 'a0000000-0000-4000-8000-000000000002']);

    const freya = await screen.findByRole('link', {name: 'Freya Ahmed'});
    expect(freya).toHaveAttribute('href', `/patients/${patientId}`);
    expect(freya).not.toHaveAttribute('aria-current');
    expect(await screen.findByRole('link', {name: 'Priya Ali'})).toBeInTheDocument();
  });

  it('marks a pinned patient as current while their pages are open', async () => {
    renderAt(`/patients/${patientId}/pathways`, [patientId]);

    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toHaveAttribute('aria-current', 'page');
    expect(screen.getAllByRole('link', {name: 'Freya Ahmed'})).toHaveLength(1);
  });

  it('unpins a patient, who then goes when their pages are not open', async () => {
    const {user} = renderAt('/pathways', [patientId]);

    await user.click(await rowMenuItem(user, 'Freya Ahmed', 'Unpin'));

    expect(screen.queryByRole('link', {name: 'Freya Ahmed'})).not.toBeInTheDocument();
    expect(pinnedIds('patients')).toEqual([]);
  });

  it('stops pinning at five, saying why', async () => {
    const five = ['1', '2', '3', '4', '5'].map((n) => `d000000${n}-0000-4000-8000-000000000000`);
    // They exist, so they stay pinned.
    five.forEach((id, i) => (patients[id] = ['Test', `Patient ${i + 1}`]));
    const {user} = renderAt(`/patients/${patientId}`, five);

    // Keyboard focus shows a row's menu button too, so it can be reached with Tab.
    const row = await screen.findByRole('link', {name: 'Freya Ahmed'});
    act(() => row.focus());
    expect(await screen.findByRole('button', {name: 'Freya Ahmed options'})).toBeInTheDocument();

    const pin = await rowMenuItem(user, 'Freya Ahmed', 'Pin');
    expect(isDisabled(pin)).toBe(true);
    expect(pin).toHaveTextContent(/you can pin up to 5 patients/i);
  });

  it('drops a pinned patient who no longer exists', async () => {
    const gone = 'b0000000-0000-4000-8000-000000000009';
    renderAt('/pathways', [patientId, gone]);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    await waitFor(() => expect(pinnedIds('patients')).toEqual([patientId]));
  });

  it('labels every pinned patient from one labels lookup, never opening their records (which would record a view)', async () => {
    const {fetch} = renderAt('/pathways', [patientId, 'a0000000-0000-4000-8000-000000000002']);

    await screen.findByRole('link', {name: 'Priya Ali'});
    const requests = calls(fetch);
    const lookups = requests.filter((r) => r.startsWith('GET /api/patients/labels'));
    expect(lookups).toHaveLength(1);
    expect(lookups[0]).toContain(`ids=${patientId}`);
    expect(requests.some((r) => /^GET \/api\/patients\/[0-9a-f-]{36}/.test(r))).toBe(false);
  });
});

describe('openPathwayId', () => {
  it.each([
    [`/pathways/${patientId}`, patientId],
    ['/pathways', null],
    [`/patients/${patientId}/pathways`, null],
  ])('pathname %s has open pathway %s', (pathname, expected) => {
    expect(openPathwayId(pathname)).toBe(expected);
  });
});

describe('AppSideNav pinned pathways', () => {
  const knee = 'c0000000-0000-4000-8000-000000000001';
  const scope = 'c0000000-0000-4000-8000-000000000002';
  const pathways: Record<string, {title: string; patientName: string}> = {
    [knee]: {title: 'Total knee replacement', patientName: 'Freya Ahmed'},
    [scope]: {title: 'Bronchoscopy', patientName: 'Priya Ali'},
  };

  function renderAt(path: string, pinned: string[] = []) {
    if (pinned.length) localStorage.setItem(PINS_KEY, JSON.stringify({pathways: pinned}));
    const fetch = mockApi({'GET /api/pathways/patient-pathways/labels': pathwayLabels(pathways)});
    return {fetch, ...renderWithApp(<AppSideNav />, {user: staffUser, path})};
  }

  it("labels the open pathway with the patient's initials and the pathway's acronym, and pins it", async () => {
    const {user} = renderAt(`/pathways/${knee}`);

    const item = await screen.findByRole('link', {name: 'FA TKR'});
    expect(item).toHaveAttribute('href', `/pathways/${knee}`);
    expect(item).toHaveAttribute('aria-current', 'page');

    // No tooltip with the full name; the row's menu (its button shown on hover) has it in its name.
    await user.hover(item);
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
    await user.click(await rowMenuItem(user, 'FA TKR', 'Pin', 'Total knee replacement for Freya Ahmed'));

    expect(pinnedIds('pathways')).toEqual([knee]);
  });

  it('keeps pinned pathways under Pathways on other pages, apart from pinned patients', async () => {
    renderAt('/patients', [knee, scope]);

    expect(await screen.findByRole('link', {name: 'FA TKR'})).not.toHaveAttribute('aria-current');
    expect(await screen.findByRole('link', {name: 'PA B'})).toBeInTheDocument();
    expect(pinnedIds('patients')).toEqual([]);
  });

  it('drops a pinned pathway that no longer exists', async () => {
    const gone = 'c0000000-0000-4000-8000-000000000009';
    renderAt('/', [knee, gone]);

    await screen.findByRole('link', {name: 'FA TKR'});
    await waitFor(() => expect(pinnedIds('pathways')).toEqual([knee]));
  });

  it('labels every pinned pathway from one labels lookup, never opening the pathways (which would record a view)', async () => {
    const {fetch} = renderAt('/patients', [knee, scope]);

    await screen.findByRole('link', {name: 'PA B'});
    const requests = calls(fetch);
    expect(requests.filter((r) => r.startsWith('GET /api/pathways/patient-pathways/labels'))).toHaveLength(1);
    expect(requests.some((r) => /^GET \/api\/pathways\/patient-pathways\/[0-9a-f-]{36}/.test(r))).toBe(false);
  });
});

describe('AppSideNav folded, and its menus', () => {
  it('folds to icons for staff, with no admin pages', async () => {
    renderWithApp(<AppSideNav collapsible={{isCollapsed: true}} />, {user: staffUser});

    expect(await screen.findByRole('link', {name: 'MedApp'})).toHaveAttribute('href', '/');
    expect(screen.getByRole('link', {name: 'Patients'})).toHaveAttribute('href', '/patients');
    expect(screen.getByRole('button', {name: 'Logout'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: /^Notifications/})).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Users'})).not.toBeInTheDocument();
  });

  it("holds a folded nav's row out while its menu is pinned, and lets it go when the menu closes", async () => {
    localStorage.setItem('medapp.navFlyoutDelay', 'none');
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav collapsible={{isCollapsed: true}} />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    const icon = await screen.findByRole('link', {name: 'My Worklist'});
    await user.hover(icon);
    const button = await screen.findByRole('button', {name: 'My Worklist options', hidden: true});
    await waitFor(() => expect(button).toBeVisible());
    fireEvent.pointerDown(button);
    fireEvent.click(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));

    // Off the nav altogether, well past the row's own wait: the row and its menu stay.
    await user.unhover(icon);
    await user.hover(document.body);
    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(button).toBeVisible();
    expect(button).toHaveAttribute('aria-expanded', 'true');

    // Closing the menu lets the row go, as the pointer is no longer on it.
    await user.keyboard('{Escape}');
    await waitFor(() => expect(button).not.toBeVisible());
  });

  it('stays signed in when logging out is cancelled', async () => {
    const fetch = mockFetch(() => new Response(null, {status: 204}));
    const {user, router} = renderWithApp(<AppSideNav />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /logout/i}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(router.state.location.pathname).toBe('/');
    expect(fetch.mock.calls.some(([request]) => new URL((request as Request).url).pathname === '/api/auth/logout')).toBe(false);
  });

  it("pins a row's menu open with a press of its button, so the pointer leaving doesn't close it; a second press closes it", async () => {
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    await user.hover(await screen.findByRole('link', {name: 'My Worklist'}));
    const button = await screen.findByRole('button', {name: 'My Worklist options'});
    // Pressed at once, before resting on it would open it.
    fireEvent.pointerDown(button);
    fireEvent.click(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));

    // The pointer leaving, for longer than an unpinned menu waits: still open.
    await user.unhover(button);
    await user.hover(document.body);
    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(button).toHaveAttribute('aria-expanded', 'true');

    // A second press closes it.
    await user.click(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'false'));

    // Pressed again, it opens, and its items work.
    fireEvent.pointerDown(button);
    fireEvent.click(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    const menu = document.querySelector<HTMLElement>(`[aria-labelledby="${CSS.escape(button.id)}"]`)!;
    await user.click(within(menu).getByRole('menuitem', {name: /^Open in half view/, hidden: true}));
    expect(halfViews.open).toHaveBeenCalledWith('/tasks');
  });

  it('pins a menu that the pointer resting opened, when its button is pressed', async () => {
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    await user.hover(await screen.findByRole('link', {name: 'My Worklist'}));
    const button = await screen.findByRole('button', {name: 'My Worklist options'});
    await user.hover(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    // Open from resting on it: a press pins it rather than closing it.
    await user.click(button);
    expect(button).toHaveAttribute('aria-expanded', 'true');
    await user.unhover(button);
    await user.hover(document.body);
    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(button).toHaveAttribute('aria-expanded', 'true');

    // Escape closes it, and it's let go: resting on the button opens it again, and leaving closes it.
    await user.keyboard('{Escape}');
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'false'));
    await user.hover(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    await user.unhover(button);
    await user.hover(document.body);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'false'));
  });

  it('keeps the button the menu\'s invoker through a press that pins it, so the browser\'s light dismiss leaves it open', async () => {
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    await user.hover(await screen.findByRole('link', {name: 'My Worklist'}));
    const button = await screen.findByRole('button', {name: 'My Worklist options'});
    await user.hover(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    const popover = document.getElementById(button.getAttribute('aria-controls')!)!.closest('[popover]')!;

    fireEvent.pointerDown(button);
    expect(button).toHaveAttribute('popovertarget', popover.id);
    fireEvent.pointerUp(button);
    await waitFor(() => expect(button).not.toHaveAttribute('popovertarget'));
    fireEvent.click(button);
    expect(button).toHaveAttribute('aria-expanded', 'true');
  });

  it('lets a menu go once a second click closes it, so resting on the button then opens it only for a while', async () => {
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    await user.hover(await screen.findByRole('link', {name: 'My Worklist'}));
    const button = await screen.findByRole('button', {name: 'My Worklist options'});
    // A real press and release (the menu opens on the press), at once, before resting on it would open it.
    await user.click(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    await user.click(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'false'));

    // Resting on it again opens it, unpinned: leaving closes it.
    await user.unhover(button);
    await user.hover(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    await user.unhover(button);
    await user.hover(document.body);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'false'));
  });

  it("doesn't pin a menu that a press didn't open (a right-click)", async () => {
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    await user.hover(await screen.findByRole('link', {name: 'My Worklist'}));
    const button = await screen.findByRole('button', {name: 'My Worklist options'});
    fireEvent.pointerDown(button, {button: 2, buttons: 2, pointerType: 'mouse'});
    fireEvent.pointerUp(button, {button: 2, pointerType: 'mouse'});
    expect(button).toHaveAttribute('aria-expanded', 'false');
    // Resting on it opens it after a moment, unpinned: leaving closes it.
    await user.hover(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    await user.unhover(button);
    await user.hover(document.body);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'false'));
  });

  it('closes a pinned menu with Enter on its button', async () => {
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    await user.hover(await screen.findByRole('link', {name: 'My Worklist'}));
    const button = await screen.findByRole('button', {name: 'My Worklist options'});
    await user.click(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    button.focus();
    await user.keyboard('{Enter}');
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'false'));
  });

  it('pins a menu opened from the keyboard', async () => {
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    await user.hover(await screen.findByRole('link', {name: 'My Worklist'}));
    const button = await screen.findByRole('button', {name: 'My Worklist options'});
    button.focus();
    await user.keyboard('{Enter}');
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    await user.unhover(button);
    await user.hover(document.body);
    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(button).toHaveAttribute('aria-expanded', 'true');
  });

  it("places a row's menu beside the nav, level with the row's highlight", async () => {
    // jsdom has no layout: a nav 240 wide, a row inset 8 from its edges at 100 down, and the menu where it opens.
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      if (this.matches('.astryx-side-nav')) return new DOMRect(0, 0, 240, 800);
      if (this.matches('.astryx-side-nav-item')) return new DOMRect(8, 100, 224, 40);
      if (this.matches('[popover], [role="menu"]')) return new DOMRect(300, 90, 200, 120);
      return new DOMRect();
    });
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    const item = await rowMenuItem(user, 'My Worklist', 'Open in half view');
    const panel = item.closest<HTMLElement>('[popover]') ?? item.closest<HTMLElement>('[role="menu"]')!;

    // Out from the nav's edge by the row's inset (8), and level with the row.
    await waitFor(() => expect(panel.style.translate).toBe(`${240 + 8 - 300}px ${100 - 90}px`));
    expect(panel.closest<HTMLElement>('[data-astryx-layer-root]')?.style.colorScheme ?? panel.style.colorScheme).toBe('dark');
  });

  it('measures nothing more once the menu has closed, though its animation finishes after', async () => {
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      if (this.matches('.astryx-side-nav')) return new DOMRect(0, 0, 240, 800);
      if (this.matches('.astryx-side-nav-item')) return new DOMRect(8, 100, 224, 40);
      if (this.matches('button[aria-label$=" options"]')) return new DOMRect(200, 104, 28, 32);
      if (this.matches('[popover], [role="menu"], .astryx-popover')) return new DOMRect(300, 100, 200, 120);
      return new DOMRect();
    });
    let settle = () => {};
    const sliding = {finished: new Promise<void>((resolve) => (settle = resolve)), effect: {getComputedTiming: () => ({endTime: 200})}};
    const prototype = HTMLElement.prototype as unknown as {getAnimations?: () => unknown[]};
    prototype.getAnimations = () => [sliding];
    try {
      const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
      const {user} = renderWithApp(
        <HalfViewsContext.Provider value={halfViews}>
          <AppSideNav />
        </HalfViewsContext.Provider>,
        {user: staffUser},
      );
      await rowMenuItem(user, 'My Worklist', 'Open in half view');
      const button = screen.getByRole('button', {name: 'My Worklist options'});
      await waitFor(() => expect(document.querySelector('[data-nav-bridge]')).not.toBeNull());

      // Closed before the menu settles: no strip, and none comes back when the animation ends.
      await user.keyboard('{Escape}');
      await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'false'));
      await act(async () => settle());
      expect(document.querySelector('[data-nav-bridge]')).toBeNull();
    } finally {
      delete prototype.getAnimations;
    }
  });

  it("shows a row's buttons on hover alone in a browser without :focus-visible", async () => {
    // A half view to open is what gives a row its menu button.
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );
    const tasks = (await screen.findAllByRole('link', {name: 'My Worklist'}))[0];
    // Only this link's check for `:focus-visible` throws, as it would in a browser without it.
    const matches = tasks.matches.bind(tasks);
    tasks.matches = (selector: string) => {
      if (selector === ':focus-visible') throw new SyntaxError('unsupported');
      return matches(selector);
    };

    // Focus doesn't bring the buttons out (keyboard focus can't be told from a click's), but hovering does.
    act(() => tasks.focus());
    expect(screen.queryByRole('button', {name: 'My Worklist options'})).not.toBeInTheDocument();
    await user.hover(tasks);
    expect(await screen.findByRole('button', {name: 'My Worklist options'})).toBeInTheDocument();
  });

  it('measures the strip again once the menu has slid into place, ignoring an animation that never ends', async () => {
    // The menu starts at 300 while it animates, and settles at 310.
    let menuLeft = 300;
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      if (this.matches('.astryx-side-nav')) return new DOMRect(0, 0, 240, 800);
      if (this.matches('.astryx-side-nav-item')) return new DOMRect(8, 100, 224, 40);
      if (this.matches('button[aria-label$=" options"]')) return new DOMRect(200, 104, 28, 32);
      if (this.matches('[popover], [role="menu"], .astryx-popover')) return new DOMRect(menuLeft, 100, 200, 120);
      return new DOMRect();
    });
    let settle = () => {};
    const sliding = {finished: new Promise<void>((resolve) => (settle = resolve)), effect: {getComputedTiming: () => ({endTime: 200})}};
    const endless = {finished: new Promise<void>(() => {}), effect: {getComputedTiming: () => ({endTime: Infinity})}};
    // jsdom has no Web Animations: the menu's surface reports one that ends and one that never does.
    const prototype = HTMLElement.prototype as unknown as {getAnimations?: () => unknown[]};
    prototype.getAnimations = () => [sliding, endless];
    try {
      const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
      const {user} = renderWithApp(
        <HalfViewsContext.Provider value={halfViews}>
          <AppSideNav />
        </HalfViewsContext.Provider>,
        {user: staffUser},
      );
      await rowMenuItem(user, 'My Worklist', 'Open in half view');
      const strip = () => document.querySelector<HTMLElement>('[data-nav-bridge="/tasks"]');
      // First measured mid-animation: 228 to 324 (24px into the menu at 300).
      await waitFor(() => expect(strip()?.style.width).toBe('96px'));

      // Settled 10px further on: measured again, though the endless animation never finishes.
      menuLeft = 310;
      await act(async () => settle());
      await waitFor(() => expect(strip()?.style.width).toBe('106px'));
    } finally {
      delete prototype.getAnimations;
    }
  });

  it('covers the gap between the menu button and its open menu, so the pointer crossing it keeps the menu open', async () => {
    // jsdom has no layout: the menu button ends at 228 (its row at 100, 40 tall), and the menu starts at 300.
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      if (this.matches('.astryx-side-nav')) return new DOMRect(0, 0, 240, 800);
      if (this.matches('.astryx-side-nav-item')) return new DOMRect(8, 100, 224, 40);
      if (this.matches('button[aria-label$=" options"]')) return new DOMRect(200, 104, 28, 32);
      if (this.matches('[popover], [role="menu"], .astryx-popover')) return new DOMRect(300, 100, 200, 120);
      return new DOMRect();
    });
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );
    await rowMenuItem(user, 'My Worklist', 'Open in half view');
    const button = screen.getByRole('button', {name: 'My Worklist options'});

    const bridge = await waitFor(() => {
      const strip = document.querySelector<HTMLElement>('[data-nav-bridge="/tasks"]');
      expect(strip).not.toBeNull();
      return strip!;
    });
    expect(bridge).toHaveAttribute('aria-hidden', 'true');
    // From the button's end 24px into the menu (whatever's under the menu never takes the pointer), and 8px above and
    // below both: 228 to 324 across, 92 to 228 down.
    expect(bridge.style).toMatchObject({position: 'fixed', left: '228px', top: '92px', width: '96px', height: '136px'});

    // On the strip, past the time a menu waits before closing once the pointer has left it: still open.
    await user.hover(bridge);
    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(button).toHaveAttribute('aria-expanded', 'true');

    // Off it, onto the page: it closes, and the strip goes with it.
    await user.unhover(bridge);
    await user.hover(document.body);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'false'));
    expect(document.querySelector('[data-nav-bridge]')).toBeNull();
  });
});

describe('AppSideNav with a half view open', () => {
  const pathwayId = 'c0000000-0000-4000-8000-000000000001';

  function renderWithHalf(path: string, openPath: string) {
    mockApi({
      'GET /api/patients/labels': patientLabels({[patientId]: 'Test Patient'}),
      'GET /api/pathways/patient-pathways/labels': pathwayLabels({[pathwayId]: {title: 'Test pathway', patientName: 'Test Patient'}}),
    });
    const halfViews = {open: vi.fn(), openPath, openFull: vi.fn(), close: vi.fn()};
    const result = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: adminUser, path},
    );
    return {...result, halfViews};
  }

  const rowOf = (link: HTMLElement) => link.closest<HTMLElement>('.astryx-side-nav-item')!;

  it("marks the half view's page a shade darker than the current page", async () => {
    renderWithHalf('/pathways', '/admin/audit');

    const audit = await screen.findByRole('link', {name: /audit log/i});
    expect(rowOf(audit).style.backgroundColor).toBe(HALF_VIEW_SHADE);
    expect(audit).not.toHaveAttribute('aria-current');
    expect(rowOf(screen.getByRole('link', {name: /^pathways$/i})).style.backgroundColor).toBe('');
  });

  it("marks the calendar when Requires Appointment is in the half view", async () => {
    renderWithHalf('/patients', '/calendar/requires-appointment');

    const calendar = await screen.findByRole('link', {name: 'Calendar'});
    expect(rowOf(calendar).style.backgroundColor).toBe(HALF_VIEW_SHADE);
    expect(calendar).not.toHaveAttribute('aria-current');
  });

  it("marks a patient whose page is in the half view under Patients, and a section whose list is", async () => {
    const {unmount} = renderWithHalf(`/pathways/${pathwayId}`, `/patients/${patientId}`);

    const patient = await screen.findByRole('link', {name: 'Test Patient'});
    expect(rowOf(patient).style.backgroundColor).toBe(HALF_VIEW_SHADE);
    expect(rowOf(screen.getByRole('link', {name: /^patients$/i})).style.backgroundColor).toBe('');
    // The pathway open in the main view is the current page, unshaded.
    expect(await screen.findByRole('link', {name: 'TP TP'})).toHaveAttribute('aria-current', 'page');
    unmount();

    renderWithHalf(`/pathways/${pathwayId}`, '/pathways');
    const pathways = await screen.findByRole('link', {name: /^pathways$/i});
    expect(rowOf(pathways).style.backgroundColor).toBe(HALF_VIEW_SHADE);
  });

  it('opens a clicked page on its own, but leaves a modified click to the browser', async () => {
    const {user, halfViews} = renderWithHalf('/pathways', '/admin/audit');
    const users = await screen.findByRole('link', {name: /^users$/i});

    await user.keyboard('{Control>}');
    await user.click(users);
    await user.keyboard('{/Control}');
    expect(halfViews.openFull).not.toHaveBeenCalled();

    await user.click(users);
    expect(halfViews.openFull).toHaveBeenCalledWith('/admin/users');
  });
});

describe('AppSideNav notepad', () => {
  afterEach(() => localStorage.clear());

  function NotepadShown() {
    return <p>{useNotepad()!.isOpen ? 'notepad open' : 'notepad closed'}</p>;
  }

  it('opens and closes the notepad from the account footer, as a toggle pressed while open', async () => {
    const {user} = renderWithApp(
      <NotepadProvider>
        <AppSideNav />
        <NotepadShown />
      </NotepadProvider>,
      {user: staffUser},
    );

    const notepad = await screen.findByRole('button', {name: 'Notepad'});
    expect(notepad).toHaveAttribute('aria-pressed', 'false');
    await user.click(notepad);
    expect(screen.getByText('notepad open')).toBeInTheDocument();
    const pressed = screen.getByRole('button', {name: 'Notepad', pressed: true});
    // A toggle, never the current page.
    expect(pressed).not.toHaveAttribute('aria-current');

    await user.click(pressed);
    expect(screen.getByText('notepad closed')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Notepad'})).toHaveAttribute('aria-pressed', 'false');
  });

  it('has no notepad outside the signed-in layout', async () => {
    renderWithApp(<AppSideNav />, {user: staffUser});

    await screen.findByRole('button', {name: 'Logout'});
    expect(screen.queryByRole('button', {name: 'Notepad'})).not.toBeInTheDocument();
  });

  /** The nav, its notepad, and a half view showing `openPath`. */
  function renderWithHalfView(openPath: string) {
    const halfViews = {open: vi.fn(), openPath, openFull: vi.fn(), close: vi.fn()};
    const result = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <NotepadProvider>
          <AppSideNav />
          <NotepadShown />
        </NotepadProvider>
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );
    return {...result, halfViews};
  }

  it('closes the half view showing the notepad before it docks the notepad, so it is not open twice side by side', async () => {
    const {user, halfViews} = renderWithHalfView('/notepad');

    await user.click(await screen.findByRole('button', {name: 'Notepad'}));

    expect(halfViews.close).toHaveBeenCalledTimes(1);
    expect(screen.getByText('notepad open')).toBeInTheDocument();
  });

  it('leaves a half view showing another page open as it docks the notepad', async () => {
    const {user, halfViews} = renderWithHalfView('/tasks');

    await user.click(await screen.findByRole('button', {name: 'Notepad'}));

    expect(halfViews.close).not.toHaveBeenCalled();
    expect(screen.getByText('notepad open')).toBeInTheDocument();
  });

  /** The Notepad row's menu, opened from its button (the row is a button, not a link), and its items. */
  async function notepadMenuItems(openPath: string | null) {
    const halfViews = {open: vi.fn(), openPath, openFull: vi.fn(), close: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <NotepadProvider>
          <AppSideNav />
        </NotepadProvider>
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );
    await user.hover(await screen.findByRole('button', {name: 'Notepad'}));
    const button = await screen.findByRole('button', {name: 'Notepad options'});
    await user.hover(button);
    const menu = await waitFor(() => {
      expect(button).toHaveAttribute('aria-expanded', 'true');
      return document.querySelector<HTMLElement>(`[aria-labelledby="${CSS.escape(button.id)}"]`)!;
    });
    return {user, halfViews, items: within(menu).getAllByRole('menuitem', {hidden: true})};
  }

  it('opens the notepad in the half view from its menu', async () => {
    const {user, halfViews, items} = await notepadMenuItems(null);

    expect(items.map((item) => item.textContent)).toEqual(['Open in half view']);
    await user.click(items[0]);

    expect(halfViews.open).toHaveBeenCalledWith('/notepad');
  });

  it('offers only the right with a half view open, as the notepad has no page of its own in the main view', async () => {
    const {user, halfViews, items} = await notepadMenuItems('/tasks');

    expect(items.map((item) => item.textContent)).toEqual(['Open on the right']);
    await user.click(items[0]);

    expect(halfViews.open).toHaveBeenCalledWith('/notepad');
    expect(halfViews.openFull).not.toHaveBeenCalled();
  });

  it('greys out the right while the half view already shows the notepad', async () => {
    const {items} = await notepadMenuItems('/notepad');

    expect(isDisabled(items[0])).toBe(true);
    expect(items[0]).toHaveAccessibleName(/Notepad is already open on the right/);
  });
});
