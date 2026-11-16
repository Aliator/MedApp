import {act, fireEvent, screen, waitFor, within} from '@testing-library/react';
import {HalfViewsContext} from '../pages/halfView';
import {adminUser, isDisabled, json, mockApi, mockFetch, openDialog, renderWithApp, rowMenuItem, staffUser} from '../../test/render';
import {AppSideNav, HALF_VIEW_SHADE, isCurrentSection, openPatientId, openProcedureId} from './AppSideNav';
import {pinsKey} from './pins';

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
    expect(screen.getByRole('link', {name: 'Stages'})).toHaveAttribute('href', '/admin/stages');
    expect(screen.getByRole('link', {name: /templates/i})).toHaveAttribute('href', '/admin/templates');
    expect(screen.getByRole('link', {name: /audit log/i})).toHaveAttribute('href', '/admin/audit');
    expect(screen.getByRole('link', {name: 'Insights'})).toHaveAttribute('href', '/admin/insights');
    expect(screen.getByText('test-admin')).toBeInTheDocument();
    expect(screen.getByText('Administrator')).toBeInTheDocument();
  });

  it('hides the admin pages from other users', async () => {
    renderWithApp(<AppSideNav />, {user: staffUser});

    expect(await screen.findByRole('link', {name: /patients/i})).toHaveAttribute('href', '/patients');
    expect(screen.queryByRole('link', {name: /^users$/i})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /audit log/i})).not.toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
  });

  it('marks the current section', async () => {
    renderWithApp(<AppSideNav />, {user: adminUser, path: '/patients/add'});

    expect(await screen.findByRole('link', {name: /patients/i})).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', {name: 'My Tasks'})).not.toHaveAttribute('aria-current');
    // Home is the MedApp heading's, not a nav item.
    expect(screen.getByRole('link', {name: 'MedApp'})).toHaveAttribute('href', '/');
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
    [`/patients/${patientId}/procedures`, patientId],
    ['/patients', null],
    ['/patients/add', null],
    ['/procedures', null],
  ])('pathname %s has open patient %s', (pathname, expected) => {
    expect(openPatientId(pathname)).toBe(expected);
  });
});

describe('AppSideNav with a patient open', () => {
  function renderAt(path: string) {
    mockApi({
      [`GET /api/patients/${patientId}`]: () =>
        json({id: patientId, firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', email: 'f@example.com'}),
    });
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
    if (pinned.length) localStorage.setItem(pinsKey('patients', staffUser.username), JSON.stringify(pinned));
    mockApi({
      'GET /api/patients/*': (request) => {
        const id = new URL(request.url).pathname.split('/').pop()!;
        const name = patients[id];
        return name
          ? json({id, firstName: name[0], lastName: name[1], dateOfBirth: '1976-12-01', email: 'p@example.com'})
          : json({title: 'Not found'}, 404);
      },
    });
    return renderWithApp(<AppSideNav />, {user: staffUser, path});
  }

  it("pins the open patient, from the row's menu (its button shown while the row is hovered)", async () => {
    const {user} = renderAt(`/patients/${patientId}`);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    expect(screen.queryByRole('button', {name: 'Freya Ahmed options'})).not.toBeInTheDocument();
    await user.click(await rowMenuItem(user, 'Freya Ahmed', 'Pin'));

    expect(JSON.parse(localStorage.getItem(pinsKey('patients', staffUser.username))!)).toEqual([patientId]);
    await user.unhover(screen.getByRole('link', {name: 'Freya Ahmed'}));
    expect(await rowMenuItem(user, 'Freya Ahmed', 'Unpin')).toBeInTheDocument();
  });

  it("folds the patients under Patients away from its row's menu", async () => {
    const {user} = renderAt('/procedures', [patientId]);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    await user.click(await rowMenuItem(user, 'Patients', 'Hide pinned and open patients'));
    expect(screen.queryByRole('link', {name: 'Freya Ahmed'})).not.toBeInTheDocument();

    await user.unhover(screen.getByRole('link', {name: 'Patients'}));
    await user.click(await rowMenuItem(user, 'Patients', 'Show pinned and open patients'));
    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toBeInTheDocument();
  });

  it('keeps pinned patients under Patients on other pages', async () => {
    renderAt('/procedures', [patientId, 'a0000000-0000-4000-8000-000000000002']);

    const freya = await screen.findByRole('link', {name: 'Freya Ahmed'});
    expect(freya).toHaveAttribute('href', `/patients/${patientId}`);
    expect(freya).not.toHaveAttribute('aria-current');
    expect(await screen.findByRole('link', {name: 'Priya Ali'})).toBeInTheDocument();
  });

  it('marks a pinned patient as current while their pages are open', async () => {
    renderAt(`/patients/${patientId}/procedures`, [patientId]);

    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toHaveAttribute('aria-current', 'page');
    expect(screen.getAllByRole('link', {name: 'Freya Ahmed'})).toHaveLength(1);
  });

  it('unpins a patient, who then goes when their pages are not open', async () => {
    const {user} = renderAt('/procedures', [patientId]);

    await user.click(await rowMenuItem(user, 'Freya Ahmed', 'Unpin'));

    expect(screen.queryByRole('link', {name: 'Freya Ahmed'})).not.toBeInTheDocument();
    expect(JSON.parse(localStorage.getItem(pinsKey('patients', staffUser.username))!)).toEqual([]);
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
    renderAt('/procedures', [patientId, gone]);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    await waitFor(() => expect(JSON.parse(localStorage.getItem(pinsKey('patients', staffUser.username))!)).toEqual([patientId]));
  });
});

describe('openProcedureId', () => {
  it.each([
    [`/procedures/${patientId}`, patientId],
    ['/procedures', null],
    [`/patients/${patientId}/procedures`, null],
  ])('pathname %s has open procedure %s', (pathname, expected) => {
    expect(openProcedureId(pathname)).toBe(expected);
  });
});

describe('AppSideNav pinned procedures', () => {
  const knee = 'c0000000-0000-4000-8000-000000000001';
  const scope = 'c0000000-0000-4000-8000-000000000002';
  const procedures: Record<string, {title: string; patientName: string}> = {
    [knee]: {title: 'Total knee replacement', patientName: 'Freya Ahmed'},
    [scope]: {title: 'Bronchoscopy', patientName: 'Priya Ali'},
  };

  function renderAt(path: string, pinned: string[] = []) {
    if (pinned.length) localStorage.setItem(pinsKey('procedures', staffUser.username), JSON.stringify(pinned));
    mockApi({
      'GET /api/procedures/patient-procedures/*': (request) => {
        const id = new URL(request.url).pathname.split('/').pop()!;
        return procedures[id] ? json({id, ...procedures[id]}) : json({title: 'Not found'}, 404);
      },
    });
    return renderWithApp(<AppSideNav />, {user: staffUser, path});
  }

  it("labels the open procedure with the patient's initials and the procedure's acronym, and pins it", async () => {
    const {user} = renderAt(`/procedures/${knee}`);

    const item = await screen.findByRole('link', {name: 'FA TKR'});
    expect(item).toHaveAttribute('href', `/procedures/${knee}`);
    expect(item).toHaveAttribute('aria-current', 'page');

    // No tooltip with the full name; the row's menu (its button shown on hover) has it in its name.
    await user.hover(item);
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
    await user.click(await rowMenuItem(user, 'FA TKR', 'Pin', 'Total knee replacement for Freya Ahmed'));

    expect(JSON.parse(localStorage.getItem(pinsKey('procedures', staffUser.username))!)).toEqual([knee]);
  });

  it('keeps pinned procedures under Procedures on other pages, apart from pinned patients', async () => {
    renderAt('/patients', [knee, scope]);

    expect(await screen.findByRole('link', {name: 'FA TKR'})).not.toHaveAttribute('aria-current');
    expect(await screen.findByRole('link', {name: 'PA B'})).toBeInTheDocument();
    expect(localStorage.getItem(pinsKey('patients', staffUser.username))).toBeNull();
  });

  it('drops a pinned procedure that no longer exists', async () => {
    const gone = 'c0000000-0000-4000-8000-000000000009';
    renderAt('/', [knee, gone]);

    await screen.findByRole('link', {name: 'FA TKR'});
    await waitFor(() => expect(JSON.parse(localStorage.getItem(pinsKey('procedures', staffUser.username))!)).toEqual([knee]));
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

  it('stays signed in when logging out is cancelled', async () => {
    const fetch = mockFetch(() => new Response(null, {status: 204}));
    const {user, router} = renderWithApp(<AppSideNav />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /logout/i}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(router.state.location.pathname).toBe('/');
    expect(fetch.mock.calls.some(([request]) => new URL((request as Request).url).pathname === '/api/auth/logout')).toBe(false);
  });

  it("opens a row's menu with a press of its button, and a second press leaves it open", async () => {
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    await user.hover(await screen.findByRole('link', {name: 'My Tasks'}));
    const button = await screen.findByRole('button', {name: 'My Tasks options'});
    // Pressed at once, before resting on it would open it.
    fireEvent.pointerDown(button);
    fireEvent.click(button);
    await waitFor(() => expect(button).toHaveAttribute('aria-expanded', 'true'));
    await user.click(button);
    expect(button).toHaveAttribute('aria-expanded', 'true');

    const menu = document.querySelector<HTMLElement>(`[aria-labelledby="${CSS.escape(button.id)}"]`)!;
    await user.click(within(menu).getByRole('menuitem', {name: /^Open in half view/, hidden: true}));
    expect(halfViews.open).toHaveBeenCalledWith('/tasks');
  });

  it("places a row's menu beside the nav, level with the row's highlight", async () => {
    // jsdom has no layout: a nav 240 wide, a row inset 8 from its edges at 100 down, and the menu where it opens.
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      if (this.matches('.astryx-side-nav')) return new DOMRect(0, 0, 240, 800);
      if (this.matches('.astryx-side-nav-item')) return new DOMRect(8, 100, 224, 40);
      if (this.matches('[popover], [role="menu"]')) return new DOMRect(300, 90, 200, 120);
      return new DOMRect();
    });
    const halfViews = {open: vi.fn(), openPath: null, openFull: vi.fn()};
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <AppSideNav />
      </HalfViewsContext.Provider>,
      {user: staffUser},
    );

    const item = await rowMenuItem(user, 'My Tasks', 'Open in half view');
    const panel = item.closest<HTMLElement>('[popover]') ?? item.closest<HTMLElement>('[role="menu"]')!;

    // Out from the nav's edge by the row's inset (8), and level with the row.
    await waitFor(() => expect(panel.style.translate).toBe(`${240 + 8 - 300}px ${100 - 90}px`));
    expect(panel.closest<HTMLElement>('[data-astryx-layer-root]')?.style.colorScheme ?? panel.style.colorScheme).toBe('dark');
  });
});

describe('AppSideNav with a half view open', () => {
  const procedureId = 'c0000000-0000-4000-8000-000000000001';

  function renderWithHalf(path: string, openPath: string) {
    mockApi({
      [`GET /api/patients/${patientId}`]: () =>
        json({id: patientId, firstName: 'Test', lastName: 'Patient', dateOfBirth: '1980-01-01', email: 't@example.org'}),
      [`GET /api/procedures/patient-procedures/${procedureId}`]: () =>
        json({id: procedureId, title: 'Test procedure', patientName: 'Test Patient'}),
    });
    const halfViews = {open: vi.fn(), openPath, openFull: vi.fn()};
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
    renderWithHalf('/procedures', '/admin/audit');

    const audit = await screen.findByRole('link', {name: /audit log/i});
    expect(rowOf(audit).style.backgroundColor).toBe(HALF_VIEW_SHADE);
    expect(audit).not.toHaveAttribute('aria-current');
    expect(rowOf(screen.getByRole('link', {name: /^procedures$/i})).style.backgroundColor).toBe('');
  });

  it("marks a patient whose page is in the half view under Patients, and a section whose list is", async () => {
    const {unmount} = renderWithHalf(`/procedures/${procedureId}`, `/patients/${patientId}`);

    const patient = await screen.findByRole('link', {name: 'Test Patient'});
    expect(rowOf(patient).style.backgroundColor).toBe(HALF_VIEW_SHADE);
    expect(rowOf(screen.getByRole('link', {name: /^patients$/i})).style.backgroundColor).toBe('');
    // The procedure open in the main view is the current page, unshaded.
    expect(await screen.findByRole('link', {name: 'TP TP'})).toHaveAttribute('aria-current', 'page');
    unmount();

    renderWithHalf(`/procedures/${procedureId}`, '/procedures');
    const procedures = await screen.findByRole('link', {name: /^procedures$/i});
    expect(rowOf(procedures).style.backgroundColor).toBe(HALF_VIEW_SHADE);
  });

  it('opens a clicked page on its own, but leaves a modified click to the browser', async () => {
    const {user, halfViews} = renderWithHalf('/procedures', '/admin/audit');
    const users = await screen.findByRole('link', {name: /^users$/i});

    await user.keyboard('{Control>}');
    await user.click(users);
    await user.keyboard('{/Control}');
    expect(halfViews.openFull).not.toHaveBeenCalled();

    await user.click(users);
    expect(halfViews.openFull).toHaveBeenCalledWith('/admin/users');
  });
});
