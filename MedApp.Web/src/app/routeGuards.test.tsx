import {screen, waitFor} from '@testing-library/react';
import {adminUser, json, mockApi, mockFetch, renderApp, signedOutUser, staffUser} from '../test/render';
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
  });

  it('shows the not-found page for an unknown address', async () => {
    renderApp({path: '/no-such-page', user: adminUser});

    expect(await screen.findByText(/nothing at this address/i)).toBeInTheDocument();
  });
});
