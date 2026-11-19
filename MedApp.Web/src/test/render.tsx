import {QueryClient} from '@tanstack/react-query';
import {createMemoryHistory, createRootRoute, createRoute, createRouter, Outlet, RouterProvider} from '@tanstack/react-router';
import {render, screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {StrictMode, type ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {whoamiQuery, type CurrentUser} from '../auth/auth';
import {PERMISSIONS, type Permission} from '../auth/permissions';
import {ROLE_PERMISSIONS} from './roles';

export {ROLE_PERMISSIONS};
import {routeTree} from '../routeTree.gen';

/** A signed-in member of staff with these roles, and the permissions they grant together. */
export const userWithRoles = (username: string, ...roles: (keyof typeof ROLE_PERMISSIONS)[]): CurrentUser => ({
  isAuthenticated: true,
  username,
  roles,
  permissions: PERMISSIONS.filter((p) => roles.some((role) => (ROLE_PERMISSIONS[role] as readonly Permission[]).includes(p))),
  mustChangePassword: false,
  mustSetUpTwoFactor: false,
});

export const adminUser: CurrentUser = userWithRoles('test-admin', 'Admin', 'All staff (before roles)');
export const staffUser: CurrentUser = userWithRoles('test-user', 'All staff (before roles)');
/** A manager: assigns work, arranges meetings, Insights and monitoring, but works on no pathway and books nothing. */
export const managerUser: CurrentUser = userWithRoles('test-manager', 'Manager');
/** A nurse: works on pathways and books, but can't assign, unlock or delete. */
export const nurseUser: CurrentUser = userWithRoles('test-nurse', 'Nurse');
/** A senior clinician: unlocks stages and finishes one without its required visit, which the built-in Admin role can't. */
export const seniorClinicianUser: CurrentUser = userWithRoles('test-senior', 'Senior clinician');
/** A staff member signed in with a password an admin gave them, which they must change before anything else. */
export const mustChangePasswordUser: CurrentUser = {...staffUser, username: 'test-new-user', mustChangePassword: true};
/** An admin held to setting up two-factor authentication, which Admin Settings requires of admins. */
export const mustSetUpTwoFactorUser: CurrentUser = {...adminUser, username: 'test-held-admin', mustSetUpTwoFactor: true};
export const signedOutUser: CurrentUser = {
  isAuthenticated: false,
  username: '',
  roles: [],
  permissions: [],
  mustChangePassword: false,
  mustSetUpTwoFactor: false,
};

function testQueryClient(user?: CurrentUser) {
  const queryClient = new QueryClient({defaultOptions: {queries: {retry: false}, mutations: {retry: false}}});
  if (user) queryClient.setQueryData(whoamiQuery.queryKey, user);
  return queryClient;
}

/**
 * Renders one component with the app's providers, inside a router that shows it at any path, so components
 * that link or navigate work. `user` pre-fills the whoami cache as the signed-in layout's guard would.
 * `strict` renders it all inside StrictMode, as the app starts (`entry-client`), so effects run twice as they do in
 * development. It has to be outermost: a StrictMode mounted inside the router doesn't run the effects again.
 */
export function renderWithApp(ui: ReactNode, {path = '/', user, strict = false}: {path?: string; user?: CurrentUser; strict?: boolean} = {}) {
  const queryClient = testQueryClient(user);
  const rootRoute = createRootRoute({component: Outlet});
  const page = () => <>{ui}</>;
  const routes = rootRoute.addChildren([
    createRoute({getParentRoute: () => rootRoute, path: '/', component: page}),
    createRoute({getParentRoute: () => rootRoute, path: '$', component: page}),
  ]);
  const router = createRouter({routeTree: routes, history: createMemoryHistory({initialEntries: [path]})});
  const app = (
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router as never} />
    </AppProviders>
  );
  const result = render(strict ? <StrictMode>{app}</StrictMode> : app);
  return {...result, router, queryClient, user: userEvent.setup()};
}

/**
 * Renders the whole app (real routes and guards) starting at `path`. `scrollRestoration` turns on the router's, as the
 * app's own router has it, for tests of what it does to scrolled elements.
 */
export function renderApp({path = '/', user, scrollRestoration = false}: {path?: string; user?: CurrentUser; scrollRestoration?: boolean} = {}) {
  const queryClient = testQueryClient(user);
  const router = createRouter({
    routeTree,
    context: {queryClient},
    history: createMemoryHistory({initialEntries: [path]}),
    scrollRestoration,
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

/**
 * Opens a side nav row's menu, as a person does: the pointer on the row (which shows the menu's button), then on
 * the button (which opens the menu after a moment). `row` is the row's link name; `label` names the menu (the
 * row's full name, e.g. a pathway's), if different. Returns the menu's item with the given name.
 */
export async function rowMenuItem(user: ReturnType<typeof userEvent.setup>, row: string, item: RegExp | string, label = row) {
  // The nav's link comes first (a folded nav's icon, whose row then comes out with the menu's button).
  await user.hover((await screen.findAllByRole('link', {name: row}))[0]);
  const buttons = await screen.findAllByRole('button', {name: `${label} options`});
  const button = buttons[buttons.length - 1];
  await user.hover(button);
  // Every row's menu is in the page, closed ones too: this one is the menu its button names.
  const menu = await waitFor(() => {
    const element = document.querySelector<HTMLElement>(`[aria-labelledby="${CSS.escape(button.id)}"]`);
    expect(button).toHaveAttribute('aria-expanded', 'true');
    return element!;
  });
  // A menu item's name may run on into its description (e.g. why it's greyed out), so it's matched from the start.
  const name = typeof item === 'string' ? (accessible: string) => accessible.startsWith(item) : item;
  return within(menu).findByRole('menuitem', {name, hidden: true});
}

/**
 * The search bar showing (its filters show in it as tokens, e.g. "Status: is Active"), or null. A list keeps its search
 * mounted but hidden while its favourites show, so the hidden one doesn't count.
 */
const shownSearch = () =>
  [...document.querySelectorAll<HTMLElement>('.astryx-power-search')].find((bar) => !bar.closest('[aria-hidden="true"]')) ?? null;

/** A list's search bar (its filters show in it as tokens, e.g. "Status: is Active"). */
export const searchBar = () => shownSearch()!;

/** Picks an item from the menu beside a list's favourites or search ("List options"): Search, Favourites or Configuration…. */
export async function pickListOption(user: ReturnType<typeof userEvent.setup>, item: string) {
  // Its name says which tab the list is showing, if any: "List options (showing Not started)".
  const button = await screen.findByRole('button', {name: /^List options/});
  await user.click(button);
  const menu = await waitFor(() => {
    const element = document.querySelector<HTMLElement>(`[aria-labelledby="${CSS.escape(button.id)}"]`);
    expect(element).not.toBeNull();
    return element!;
  });
  // An item's name runs on into its description ("Search Search with filters"), so it's matched from the start.
  await user.click(await within(menu).findByRole('menuitem', {name: (name) => name.startsWith(item), hidden: true}));
}

/** Shows a list's search bar in place of its tabs (the menu's Search), if it isn't showing already. */
export async function openSearch(user: ReturnType<typeof userEvent.setup>) {
  if (shownSearch()) return;
  await pickListOption(user, 'Search');
  await waitFor(() => expect(shownSearch()).not.toBeNull());
}

/** Picks one of a list's tabs (its favourites), going back to the tabs from its search first if need be. */
export async function pickFavourite(user: ReturnType<typeof userEvent.setup>, name: string) {
  await waitFor(() => expect(screen.queryAllByRole('tab').length > 0 || shownSearch() !== null).toBe(true));
  if (!screen.queryByRole('tab', {name})) await pickListOption(user, 'Favourites');
  await user.click(await screen.findByRole('tab', {name}));
}

/** Turns screen reader announcements on (Account Settings, Accessibility; they start off), before rendering. */
export const withAnnouncements = () => localStorage.setItem('medapp.accessibility', JSON.stringify({announcements: true}));

/** Waits for screen readers to have been told `text` (the announcer's live region; see `useAnnounce`). */
export const expectAnnounced = (text: string | RegExp) =>
  waitFor(() => expect(screen.getByTestId('announcer')).toHaveTextContent(text));

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

/** A `SummaryAlert`'s button (the status icon after its label), found by its label ("Restricted record"), with or without its summary after it. */
export const alertLabelName = (label: string) => new RegExp(`^${label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(: .+)?, show details$`);

/**
 * Opens a `SummaryAlert`'s whole message (a click on the icon after its label, e.g. "Restricted record") and returns the overlay, to
 * look inside with `within`. `scope` narrows where the label is looked for (a dialog, when a page shows the same one).
 */
export async function openAlertDetails(user: ReturnType<typeof userEvent.setup>, label: string, scope: HTMLElement = document.body) {
  await user.click(await within(scope).findByRole('button', {name: alertLabelName(label)}));
  return screen.findByRole('dialog', {name: `${label}: details`});
}

/** The description line Astryx shows under a field's label (found through the input's aria-describedby), untrimmed. */
export const descriptionOf = (input: HTMLElement) =>
  (input.getAttribute('aria-describedby') ?? '')
    .split(' ')
    .map((id) => document.getElementById(id))
    .find((element) => element !== null)?.textContent;

/** A synthetic page of results, shaped like the API's PagedResponse. */
export const pageOf = <T,>(items: T[], totalCount = items.length, page = 1) => ({
  items,
  page,
  pageSize: 10,
  totalCount,
  totalPages: Math.ceil(totalCount / 10),
});
