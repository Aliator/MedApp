import {QueryClient} from '@tanstack/react-query';
import {createMemoryHistory, createRootRoute, createRoute, createRouter, Outlet, RouterProvider} from '@tanstack/react-router';
import {render, screen, waitFor} from '@testing-library/react';
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

/**
 * Astryx keeps disabled buttons focusable, so it marks them with aria-disabled rather than the disabled
 * attribute, which jest-dom's toBeDisabled doesn't look at. This checks both.
 */
export const isDisabled = (element: HTMLElement) =>
  element.hasAttribute('disabled') || element.getAttribute('aria-disabled') === 'true';

/** The dialog that is open, or null: closed Astryx dialogs stay in the DOM without `open`. */
export const openDialog = () => document.querySelector<HTMLDialogElement>('dialog[open]');

export const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {status, headers: {'content-type': 'application/json'}});

type ApiHandler = (request: Request) => Response | Promise<Response>;

/**
 * Answers API calls by "METHOD /path" (path without the query string, e.g. "GET /api/patients/search").
 * Paths may end in * to match a prefix. Anything else gets a 404, so an unexpected call shows up in the test.
 * Returns the fetch mock, to inspect the requests made.
 */
export function mockApi(handlers: Record<string, ApiHandler>) {
  return mockFetch((request) => {
    const path = new URL(request.url).pathname;
    for (const [key, handler] of Object.entries(handlers)) {
      const [method, pattern] = key.split(' ');
      const matches = pattern.endsWith('*') ? path.startsWith(pattern.slice(0, -1)) : path === pattern;
      if (method === request.method && matches) return handler(request);
    }
    return json({title: `No mock for ${request.method} ${path}`}, 404);
  });
}

/** The requests a fetch mock received, as "METHOD /path?query". */
export const calls = (fetch: ReturnType<typeof mockFetch>) =>
  fetch.mock.calls.map(([request]) => {
    const url = new URL((request as Request).url);
    return `${(request as Request).method} ${url.pathname}${url.search}`;
  });

/**
 * Waits for an alert containing `text`. A page can have several role="alert" regions (a banner and field
 * errors), so this looks through all of them.
 */
export async function findAlert(text: string | RegExp) {
  const has = (el: HTMLElement) => (typeof text === 'string' ? el.textContent?.includes(text) : text.test(el.textContent ?? ''));
  return waitFor(() => {
    const match = screen.getAllByRole('alert').find(has);
    if (!match) throw new Error(`No alert containing ${String(text)}`);
    return match;
  });
}

/** A synthetic page of results, shaped like the API's PagedResponse. */
export const pageOf = <T,>(items: T[], totalCount = items.length, page = 1) => ({
  items,
  page,
  pageSize: 10,
  totalCount,
  totalPages: Math.ceil(totalCount / 10),
});
