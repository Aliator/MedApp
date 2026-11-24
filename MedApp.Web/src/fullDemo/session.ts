import {clearAllAppStorage} from '../app/settingsStore';
import {backendModule, type BackendModule} from './backend';

/**
 * Who's signed in, across reloads. The backend keeps its one session in memory (MedApp.Browser's
 * `BrowserSessionCookieService`, as the API keeps it in a cookie), so a reload starts it signed out: the demo remembers
 * the username for this tab, as a session cookie would last, and signs in again as them once the backend has started
 * (any password does: the account picker). Not a `medapp.` key, so signing out's clearing leaves it to the sign-out
 * itself, which forgets it.
 */
const KEY = 'medappFullDemo.user';

/** The password the account picker signs in with: the backend takes any that isn't empty. */
export const DEMO_PASSWORD = 'full-demo';

export function rememberedUser(): string | null {
  try {
    return sessionStorage.getItem(KEY);
  } catch {
    return null;
  }
}

function remember(username: string | null) {
  try {
    if (username) sessionStorage.setItem(KEY, username);
    else sessionStorage.removeItem(KEY);
  } catch {
    // Without storage, a reload signs out.
  }
}

/**
 * Keeps track of signing in and out from the requests the app sends (`install`'s answers): a sign-in that worked
 * remembers its username, signing out (or a whoami that says nobody is) forgets it.
 */
export function noteAnswer(method: string, path: string, status: number, requestBody: string | null, responseBody: string | null | undefined): void {
  const route = path.split('?')[0].toLowerCase();
  if (method === 'POST' && route === '/api/auth/login' && status === 200) {
    try {
      const {username} = JSON.parse(requestBody ?? '{}') as {username?: string};
      if (username) remember(username);
    } catch {
      // Not JSON: nothing to remember.
    }
  } else if (method === 'POST' && route === '/api/auth/logout') {
    remember(null);
  } else if (method === 'GET' && route === '/api/auth/whoami' && status === 200) {
    try {
      if ((JSON.parse(responseBody ?? '{}') as {isAuthenticated?: boolean}).isAuthenticated === false) remember(null);
    } catch {
      // Not JSON: leave it.
    }
  }
}

const JSON_HEADERS = {'Content-Type': 'application/json'};

/** Signs in again, after a reload, as whoever was signed in; forgets them if that's refused. */
export async function restoreSession(backend: BackendModule): Promise<void> {
  const username = rememberedUser();
  if (!username) return;
  const answer = await backend.fetchApi('POST', '/api/auth/login', JSON_HEADERS, JSON.stringify({username, password: DEMO_PASSWORD}));
  if (answer.status !== 200) remember(null);
}

/** The site's base address (`/MedApp/full/`), without its last slash, for full page loads. */
const base = () => import.meta.env.BASE_URL.replace(/\/$/, '');

/** Leaves this page with a full load. Its own object, so tests can stand in for the browser's. */
export const fullDemoNavigation = {
  assign: (url: string) => window.location.assign(url),
};

function closeHalfView() {
  try {
    sessionStorage.removeItem('medapp.halfView');
  } catch {
    // Without storage there's no half view kept.
  }
}

/**
 * Signs in as another account through the API (signing out first) and opens `path` (a path in the app, worked out as
 * them by `resolvePath`, as the walkthrough's records are looked up), on its own, with a full load, so every cached
 * answer is read again as them. Waits for the database to be saved first, as the load starts the backend again.
 */
export async function switchAccount(username: string, path: string, resolvePath: (path: string) => Promise<string> = async (p) => p): Promise<void> {
  const backend = await backendModule();
  await backend.fetchApi('POST', '/api/auth/logout', {});
  const answer = await backend.fetchApi('POST', '/api/auth/login', JSON_HEADERS, JSON.stringify({username, password: DEMO_PASSWORD}));
  remember(answer.status === 200 ? username : null);
  const target = await resolvePath(path).catch(() => '/');
  await backend.flush();
  closeHalfView();
  fullDemoNavigation.assign(`${base()}${target}`);
}

/**
 * Reset: forgets this browser's copy of the database, so the next start downloads the published one, and reloads at
 * `path` (who's signed in is kept, as the light demo's Reset keeps them). The browser's app storage (pins, settings,
 * list choices) goes too, as a first visit has it.
 */
export async function resetFullDemo(path = '/'): Promise<void> {
  const backend = await backendModule();
  await backend.forgetSavedDatabase();
  clearAllAppStorage();
  closeHalfView();
  fullDemoNavigation.assign(`${base()}${path}`);
}

export async function refreshFullDemo(path = '/'): Promise<void> {
  const backend = await backendModule();
  await backend.forgetSavedDatabase();
  clearAllAppStorage();
  try {
    sessionStorage.clear();
    if (typeof caches !== 'undefined') for (const name of await caches.keys()) await caches.delete(name);
    await fetch(`${base()}${path}`, {cache: 'reload'});
  } catch {
    void 0;
  }
  fullDemoNavigation.assign(`${base()}${path}`);
}
