import {QueryClient} from '@tanstack/react-query';
import {createMemoryHistory, createRootRoute, createRoute, createRouter, Outlet, RouterProvider} from '@tanstack/react-router';
import {render} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {whoamiQuery, type CurrentUser} from '../auth/auth';
import {routeTree} from '../routeTree.gen';

export const adminUser: CurrentUser = {isAuthenticated: true, username: 'test-admin', roles: ['Admin', 'User'], isAdmin: true};
export const staffUser: CurrentUser = {isAuthenticated: true, username: 'test-user', roles: ['User'], isAdmin: false};
export const signedOutUser: CurrentUser = {isAuthenticated: false, username: '', roles: [], isAdmin: false};

function testQueryClient(user?: CurrentUser) {
  const queryClient = new QueryClient({defaultOptions: {queries: {retry: false}, mutations: {retry: false}}});
  if (user) queryClient.setQueryData(whoamiQuery.queryKey, user);
  return queryClient;
}

/**
 * Renders one component with the app's providers, inside a router that shows it at any path, so components
 * that link or navigate work. `user` pre-fills the whoami cache as the signed-in layout's guard would.
 */
export function renderWithApp(ui: ReactNode, {path = '/', user}: {path?: string; user?: CurrentUser} = {}) {
  const queryClient = testQueryClient(user);
  const rootRoute = createRootRoute({component: Outlet});
  const page = () => <>{ui}</>;
  const routes = rootRoute.addChildren([
    createRoute({getParentRoute: () => rootRoute, path: '/', component: page}),
    createRoute({getParentRoute: () => rootRoute, path: '$', component: page}),
  ]);
  const router = createRouter({routeTree: routes, history: createMemoryHistory({initialEntries: [path]})});
  const result = render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router as never} />
    </AppProviders>,
  );
  return {...result, router, queryClient, user: userEvent.setup()};
}

/** Renders the whole app (real routes and guards) starting at `path`. */
export function renderApp({path = '/', user}: {path?: string; user?: CurrentUser} = {}) {
  const queryClient = testQueryClient(user);
  const router = createRouter({
    routeTree,
    context: {queryClient},
    history: createMemoryHistory({initialEntries: [path]}),
  });
  const result = render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return {...result, router, queryClient, user: userEvent.setup()};
}

/** Replaces fetch with `handler`, which gets each request the API client makes. Returns the mock to inspect calls. */
export function mockFetch(handler: (request: Request) => Response | Promise<Response>) {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) =>
    handler(input instanceof Request ? input : new Request(input, init)),
  );
  vi.stubGlobal('fetch', fn);
  return fn;
}

export const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {status, headers: {'content-type': 'application/json'}});
