import {screen, waitFor} from '@testing-library/react';
import {adminUser, json, managerUser, mockApi, mockFetch, mustChangePasswordUser, mustSetUpTwoFactorUser, nurseUser, pageOf, renderApp, signedOutUser, staffUser} from '../test/render';
import {authenticatorSetup} from '../test/twoFactor';
import {insights, templates} from '../test/insights';
import {roleResponse} from '../test/roles';

describe('route guards', () => {
  it('sends a signed-out visitor to the login page', async () => {
    const {router} = renderApp({path: '/patients', user: signedOutUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(await screen.findByRole('heading', {name: /sign in/i})).toBeInTheDocument();
  });

  it('shows a signed-in user the page inside the app shell', async () => {
    renderApp({path: '/', user: staffUser});

    expect(await screen.findByRole('heading', {name: `Welcome, ${staffUser.username}`})).toBeInTheDocument();
    expect(screen.getByRole('navigation')).toBeInTheDocument();
  });

  it('tells someone without ManageUsers that the Users page needs it, rather than showing it', async () => {
    const {router} = renderApp({path: '/admin/users', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
    expect(router.state.location.search).toEqual({permission: 'ManageUsers'});
    expect(await screen.findByRole('heading', {name: "You don't have permission to use this page"})).toBeInTheDocument();
    expect(screen.getAllByText(/It needs the permission to: Manage accounts: create and delete them/)[0]).toBeInTheDocument();
    // Inside the signed-in layout, so the side nav is still there to go elsewhere.
    expect(screen.getByRole('navigation')).toBeInTheDocument();
  });

  it('lets a manager into Insights (ViewInsights) but not the Users page', async () => {
    mockApi({'GET /api/pathways/insights': () => json({}), 'GET /api/pathway-templates*': () => json([])});
    const {router} = renderApp({path: '/admin/users', user: managerUser});
    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));

    await router.navigate({href: '/admin/insights'});
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/insights'));
  });

  it.each(['/admin/activity', '/admin/retention', '/patients/0000000d-0000-4000-8000-000000000001/export'])(
    'sends someone without the permission away from %s, without asking for its data',
    async (path) => {
      const fetch = mockFetch(() => json({items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0}));
      const {router} = renderApp({path, user: staffUser});

      await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
      expect(fetch.mock.calls.some(([r]) => /activity|retention|export/.test(new URL((r as Request).url).pathname))).toBe(false);
    },
  );

  it.each([
    ['/admin/activity', 'Activity Log'],
    ['/admin/retention', 'Retention Review'],
  ])('lets an admin into %s', async (path, heading) => {
    mockFetch(() => json({items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0}));
    renderApp({path, user: adminUser});

    expect(await screen.findByRole('heading', {name: heading, level: 1})).toBeInTheDocument();
  });

  it('lets those who answer or review data requests in, and sends anyone else away without asking for them', async () => {
    const fetch = mockApi({'GET /api/data-requests/search': () => json(pageOf([]))});
    const {router, unmount} = renderApp({path: '/data-requests', user: staffUser});
    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
    expect(router.state.location.search).toEqual({permission: 'ManagePrivacy'});
    expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname.includes('data-requests'))).toBe(false);
    unmount();

    renderApp({path: '/data-requests', user: adminUser});
    expect(await screen.findByRole('heading', {name: 'Data Requests', level: 1})).toBeInTheDocument();
  });

  it('sends someone without ManageSetup away from Closed Days, without asking for them', async () => {
    const fetch = mockApi({'GET /api/calendar/closed-days': () => json([])});
    const {router} = renderApp({path: '/admin/closed-days?year=2027', user: staffUser});
    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
    expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname.includes('closed-days'))).toBe(false);
  });

  it('lets an admin into Closed Days, on the year the address names', async () => {
    mockApi({'GET /api/calendar/closed-days': () => json([])});
    renderApp({path: '/admin/closed-days?year=2027', user: adminUser});

    expect(await screen.findByRole('heading', {name: 'Closed Days', level: 1})).toBeInTheDocument();
    expect(await screen.findByText('No closed days in 2027')).toBeInTheDocument();
  });

  it('sends someone without ManageSetup away from Sites and Rooms, without asking for them', async () => {
    const fetch = mockApi({'GET /api/sites': () => json([])});
    const {router} = renderApp({path: '/admin/sites', user: staffUser});
    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
    expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname.includes('sites'))).toBe(false);
  });

  it('lets an admin into Sites and Rooms', async () => {
    mockApi({'GET /api/sites': () => json([{id: 'main', name: 'Main site', addressLines: [], isArchived: false, canBeDeleted: false, rooms: []}])});
    renderApp({path: '/admin/sites', user: adminUser});

    expect(await screen.findByRole('heading', {name: 'Sites and Rooms', level: 1})).toBeInTheDocument();
    expect(await screen.findByText('Main site')).toBeInTheDocument();
  });

  it('sends someone without ManageSetup away from Specimen Types and Labs, and lets an admin in', async () => {
    const fetch = mockApi({'GET /api/specimen-types': () => json([]), 'GET /api/laboratories': () => json([])});
    const {router, unmount} = renderApp({path: '/admin/specimen-setup', user: staffUser});
    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
    expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname.includes('specimen'))).toBe(false);
    unmount();

    renderApp({path: '/admin/specimen-setup', user: adminUser});
    expect(await screen.findByRole('heading', {name: 'Specimen Types and Labs', level: 1})).toBeInTheDocument();
  });

  it('opens the Lab on To send for those who send specimens, asking for that view', async () => {
    const fetch = mockApi({
      // The Lab's worklist is a POST since #200, so a specimen number typed into its search stays out of the address.
      'POST /api/specimens/search': () => json({items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0}),
      'GET /api/laboratories': () => json([]),
    });
    renderApp({path: '/lab', user: nurseUser});

    expect(await screen.findByRole('heading', {name: 'Lab', level: 1})).toBeInTheDocument();
    const views = async () =>
      Promise.all(
        fetch.mock.calls
          .map(([r]) => r as Request)
          .filter((r) => r.method === 'POST' && new URL(r.url).pathname === '/api/specimens/search')
          .map(async (r) => ((await r.clone().json()) as {view?: string}).view),
      );
    await waitFor(async () => expect(await views()).toContain('ToSend'));
  });

  it('sends someone without ManageSetup away from Specialties, without asking for them', async () => {
    const fetch = mockApi({'GET /api/specialties': () => json([{id: 's1', name: 'Dermatology', isArchived: false, canBeDeleted: true}])});
    const {router} = renderApp({path: '/admin/specialties', user: staffUser});
    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
    expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname.includes('specialties'))).toBe(false);
  });

  it('lets an admin into Specialties', async () => {
    mockApi({'GET /api/specialties': () => json([{id: 's1', name: 'Dermatology', isArchived: false, canBeDeleted: true}])});
    renderApp({path: '/admin/specialties', user: adminUser});

    expect(await screen.findByRole('heading', {name: 'Specialties', level: 1})).toBeInTheDocument();
    expect(await screen.findByText('Dermatology')).toBeInTheDocument();
  });

  it.each(['/admin/letter-templates', '/admin/letter-templates/new', '/admin/letter-templates/l1'])(
    'sends someone without ManageSetup away from %s, without asking for letter templates',
    async (path) => {
      const fetch = mockApi({'GET /api/letter-templates*': () => json([]), 'GET /api/specialties': () => json([])});
      const {router} = renderApp({path, user: staffUser});
      await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
      expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname.includes('letter-templates'))).toBe(false);
    },
  );

  it('lets an admin into Letter Templates', async () => {
    mockApi({'GET /api/letter-templates': () => json([{id: 'l1', name: 'Skin clinic letter', kind: 'ClinicLetter', isArchived: false}])});
    renderApp({path: '/admin/letter-templates', user: adminUser});

    expect(await screen.findByRole('heading', {name: 'Letter Templates', level: 1})).toBeInTheDocument();
    expect(await screen.findByText('Skin clinic letter')).toBeInTheDocument();
  });

  it.each(['/admin/roles', '/admin/roles/new', '/admin/roles/role-nurse'])(
    'sends someone without ManageRoles away from %s, without asking for roles',
    async (path) => {
      const fetch = mockApi({'GET /api/roles': () => json([roleResponse('Admin')]), 'GET /api/roles/role-nurse': () => json(roleResponse('Nurse'))});
      const {router} = renderApp({path, user: managerUser});
      await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
      expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname.startsWith('/api/roles'))).toBe(false);
    },
  );

  it('lets an admin into Roles and Permissions, and a role', async () => {
    mockApi({'GET /api/roles': () => json([roleResponse('Admin'), roleResponse('Nurse')]), 'GET /api/roles/role-nurse': () => json(roleResponse('Nurse'))});
    renderApp({path: '/admin/roles', user: adminUser});

    expect(await screen.findByRole('heading', {name: 'Roles and Permissions', level: 1})).toBeInTheDocument();
    expect(await screen.findByText('Nurse')).toBeInTheDocument();
  });

  it('lets an admin into the admin pages', async () => {
    mockFetch((request) =>
      new URL(request.url).pathname === '/api/roles'
        ? json([roleResponse('Admin'), roleResponse('User')])
        : json({items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0}),
    );
    renderApp({path: '/admin/users', user: adminUser});

    expect(await screen.findByRole('heading', {name: /user management/i})).toBeInTheDocument();
  });

  it('lets any member of staff into their own calendar templates', async () => {
    const me = '0000000d-0000-4000-8000-0000000000a1';
    mockApi({
      'GET /api/calendar/staff': () => json([{userId: me, username: staffUser.username}]),
      [`GET /api/calendar/staff/${me}/day-templates`]: () => json([{id: 'dt1', ownerUserId: me, name: 'Clinic day', canEdit: true, blocks: []}]),
    });
    renderApp({path: '/calendar/templates', user: staffUser});

    expect(await screen.findByRole('heading', {name: 'My calendar templates', level: 1})).toBeInTheDocument();
    expect(await screen.findByText('Clinic day')).toBeInTheDocument();
  });

  it('sends the old admin Calendar Templates page to calendar templates, on the same tab', async () => {
    mockFetch(() => json([]));
    const {router} = renderApp({path: '/admin/calendar-templates?tab=week', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/calendar/templates'));
    expect(router.state.location.search).toEqual({tab: 'week'});
  });

  it("sends Requires Appointment's old address (Needs booking) to its new one, with the same page, sort and search", async () => {
    mockFetch(() => json(pageOf([])));
    const {router} = renderApp({path: '/calendar/needs-booking?page=2&sort=Patient&asc=false&patient=Synthetic', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/calendar/requires-appointment'));
    expect(router.state.location.search).toEqual({page: 2, sort: 'Patient', asc: false, patient: 'Synthetic'});
    expect(router.history.length).toBe(1);
  });

  it('sends someone without ViewInsights away from Insights', async () => {
    const {router} = renderApp({path: '/admin/insights', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
  });

  it('lets an admin into Insights, loading it for the period in the address', async () => {
    const fetch = mockApi({
      'GET /api/pathways/insights': () => json(insights),
      'GET /api/pathways/patient-pathway-stage/templates': () => json(templates),
    });
    renderApp({path: '/admin/insights?weeks=4', user: adminUser});

    expect(await screen.findByRole('heading', {name: 'Insights', level: 1})).toBeInTheDocument();
    expect(await screen.findByRole('link', {name: /^Overdue: 5/})).toBeInTheDocument();
    expect(fetch.mock.calls.some(([request]) => (request as Request).url.endsWith('/api/pathways/insights?Weeks=4'))).toBe(true);
    // Longer than the default: the route's chunk (with the chart library) loads during the test, slowly on a busy runner.
  }, 15_000);

  it('sends someone who must change their password to do that, before loading anything the API would refuse', async () => {
    const fetch = mockFetch((request) =>
      new URL(request.url).pathname === '/api/auth/me/settings' ? json({settings: null}) : json({title: 'Password change required'}, 403),
    );
    const {router} = renderApp({path: '/patients', user: mustChangePasswordUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/change-password'));
    expect(await screen.findByRole('heading', {name: 'Choose your password'})).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    // Only its own settings: no notifications, no patients.
    expect(fetch.mock.calls.map(([request]) => new URL((request as Request).url).pathname)).toEqual(['/api/auth/me/settings']);
  });

  it('sends an admin who must change their password from Insights to do that, without loading it', async () => {
    const fetch = mockFetch((request) =>
      new URL(request.url).pathname === '/api/auth/me/settings' ? json({settings: null}) : json({title: 'Password change required'}, 403),
    );
    const {router} = renderApp({path: '/admin/insights', user: {...adminUser, mustChangePassword: true}});

    await waitFor(() => expect(router.state.location.pathname).toBe('/change-password'));
    expect(fetch.mock.calls.map(([request]) => new URL((request as Request).url).pathname)).not.toContain('/api/pathways/insights');
  });

  it('sends someone signed out from the change-password page to sign in', async () => {
    const {router} = renderApp({path: '/change-password', user: signedOutUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
  });

  it('sends someone who needn\'t change their password from the change-password page home', async () => {
    const {router} = renderApp({path: '/change-password', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(await screen.findByRole('heading', {name: `Welcome, ${staffUser.username}`})).toBeInTheDocument();
  });

  it('sends an admin who must set up two-factor to do that, before loading anything the API would refuse', async () => {
    const fetch = mockApi({
      'GET /api/auth/me/settings': () => json({settings: null}),
      'GET /api/auth/me/two-factor': () => json({enabled: false, method: 'None', recoveryCodesLeft: 0, required: true}),
      'POST /api/auth/me/two-factor/authenticator': () => json(authenticatorSetup),
    });
    const {router} = renderApp({path: '/patients', user: mustSetUpTwoFactorUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/set-up-two-factor'));
    expect(await screen.findByRole('heading', {name: 'Set up two-factor authentication'})).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    // Only what the hold allows: their settings, their two-factor state, and a key for the app.
    await screen.findByRole('img', {name: 'QR code for your authenticator app'});
    expect(new Set(fetch.mock.calls.map(([request]) => new URL((request as Request).url).pathname))).toEqual(
      new Set(['/api/auth/me/settings', '/api/auth/me/two-factor', '/api/auth/me/two-factor/authenticator']),
    );
  });

  it('sends someone who must change their password there first, even if they must set up two-factor too', async () => {
    mockApi({'GET /api/auth/me/settings': () => json({settings: null})});
    const {router} = renderApp({path: '/patients', user: {...mustSetUpTwoFactorUser, mustChangePassword: true}});

    await waitFor(() => expect(router.state.location.pathname).toBe('/change-password'));
  });

  it.each([
    ['someone signed out to sign in', signedOutUser, '/login'],
    ['someone who must change their password there first', {...mustSetUpTwoFactorUser, mustChangePassword: true}, '/change-password'],
    ["anyone who needn't set it up home", adminUser, '/'],
  ])('sends %s from the two-factor set-up page', async (_, user, path) => {
    mockApi({'GET /api/auth/me/settings': () => json({settings: null})});
    const {router} = renderApp({path: '/set-up-two-factor', user});

    await waitFor(() => expect(router.state.location.pathname).toBe(path));
  });

  it('sends someone without ManageSetup away from Scheduled Jobs', async () => {
    const {router} = renderApp({path: '/admin/jobs', user: staffUser});
    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
  });

  it('lets an admin into Scheduled Jobs', async () => {
    mockApi({'GET /api/jobs': () => json([])});
    renderApp({path: '/admin/jobs', user: adminUser});

    expect(await screen.findByRole('heading', {name: 'Scheduled Jobs', level: 1})).toBeInTheDocument();
  });

  it('sends someone without ManageSecuritySettings away from Admin Settings', async () => {
    const {router} = renderApp({path: '/admin/settings', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/no-permission'));
  });

  it('lets an admin into Admin Settings', async () => {
    mockApi({'GET /api/auth/security-settings': () => json({requireTwoFactorForAdmins: false, adminsWithoutTwoFactor: 0})});
    renderApp({path: '/admin/settings', user: adminUser});

    expect(await screen.findByRole('heading', {name: 'Admin Settings', level: 1})).toBeInTheDocument();
  });

  it.each([
    ['/account/email', 'Email Address', {'GET /api/auth/me/email': () => json({email: null}), 'GET /api/auth/features': () => json({canSendEmail: true})}],
    ['/account/security', 'Security', {'GET /api/auth/me/two-factor': () => json({enabled: false})}],
  ])('opens %s from Account Settings', async (path, title, handlers) => {
    mockApi(handlers);
    renderApp({path, user: staffUser});

    expect(await screen.findByRole('heading', {name: title, level: 1})).toBeInTheDocument();
  });

  it.each([
    ['/forgot-password', 'Reset your password'],
    ['/reset-password', 'Choose a new password'],
    ['/confirm-email', 'Confirm your email address'],
  ])('opens %s for anyone, outside the signed-in layout', async (path, title) => {
    mockApi({'GET /api/auth/features': () => json({canSendEmail: true})});
    renderApp({path, user: signedOutUser});

    // The heading is there from the start, through the page's loading and link-reading states.
    await waitFor(() => expect(screen.getByRole('heading', {name: title, level: 1})).toBeInTheDocument());
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('shows the not-found page for an unknown address', async () => {
    renderApp({path: '/no-such-page', user: adminUser});

    expect(await screen.findByText(/nothing at this address/i)).toBeInTheDocument();
  });
});
