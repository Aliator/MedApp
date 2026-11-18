import {screen, waitFor} from '@testing-library/react';
import {adminUser, json, mockApi, mockFetch, mustChangePasswordUser, mustSetUpTwoFactorUser, pageOf, renderApp, signedOutUser, staffUser} from '../test/render';
import {authenticatorSetup} from '../test/twoFactor';
import {insights, templates} from '../test/insights';

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

  it('sends a non-admin away from the admin pages', async () => {
    const {router} = renderApp({path: '/admin/users', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
  });

  it.each(['/admin/activity', '/admin/retention', '/patients/0000000d-0000-4000-8000-000000000001/export'])(
    'sends a non-admin away from %s, without asking for its data',
    async (path) => {
      const fetch = mockFetch(() => json({items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0}));
      const {router} = renderApp({path, user: staffUser});

      await waitFor(() => expect(router.state.location.pathname).toBe('/'));
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

  it('lets an admin into the admin pages', async () => {
    mockFetch((request) =>
      new URL(request.url).pathname === '/api/auth/roles'
        ? json(['Admin', 'User'])
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

  it('sends a non-admin away from Insights', async () => {
    const {router} = renderApp({path: '/admin/insights', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
  });

  it('lets an admin into Insights, loading it for the period in the address', async () => {
    const fetch = mockApi({
      'GET /api/procedures/insights': () => json(insights),
      'GET /api/procedures/patient-procedure-stage/templates': () => json(templates),
    });
    renderApp({path: '/admin/insights?weeks=4', user: adminUser});

    expect(await screen.findByRole('heading', {name: 'Insights', level: 1})).toBeInTheDocument();
    expect(await screen.findByRole('link', {name: /^Overdue: 5/})).toBeInTheDocument();
    expect(fetch.mock.calls.some(([request]) => (request as Request).url.endsWith('/api/procedures/insights?Weeks=4'))).toBe(true);
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
    const {router} = renderApp({path: '/admin/insights', user: {...mustChangePasswordUser, roles: ['Admin', 'User'], isAdmin: true}});

    await waitFor(() => expect(router.state.location.pathname).toBe('/change-password'));
    expect(fetch.mock.calls.map(([request]) => new URL((request as Request).url).pathname)).not.toContain('/api/procedures/insights');
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

  it('sends a non-admin away from Admin Settings', async () => {
    const {router} = renderApp({path: '/admin/settings', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
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
