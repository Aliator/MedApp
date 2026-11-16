import {screen, waitFor} from '@testing-library/react';
import {adminUser, json, mockApi, mockFetch, mustChangePasswordUser, renderApp, signedOutUser, staffUser} from '../test/render';
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

  it('shows the not-found page for an unknown address', async () => {
    renderApp({path: '/no-such-page', user: adminUser});

    expect(await screen.findByText(/nothing at this address/i)).toBeInTheDocument();
  });
});
