import {clearAllAppStorage} from '../app/settingsStore';
import {db, reset, save} from './store';

/** The site's base address (`/MedApp/` on GitHub Pages), without its last slash, for full page loads. */
const base = () => import.meta.env.BASE_URL.replace(/\/$/, '');

/** Leaves this page for another in the app with a full load. Its own object, so tests can stand in for the browser's. */
export const demoNavigation = {
  assign: (url: string) => window.location.assign(url),
};

/** Closes any half view before a full load (it's kept for the browser tab, `AppLayout`), so the next page is on its own. */
function closeHalfView() {
  try {
    sessionStorage.removeItem('medapp.halfView');
  } catch {
    // Without storage there's no half view kept.
  }
}

/**
 * Signs in as another demo account and opens `path` (in the app, such as `/calendar`), on its own, with a full load, so
 * every cached answer is read again as them.
 */
export async function switchAccount(username: string, path = '/'): Promise<void> {
  db().user = username;
  await save();
  closeHalfView();
  demoNavigation.assign(`${base()}${path}`);
}

/** Reset: clears the demo's data, seeds it again (keeping who's signed in) and reloads at the start page. */
export async function resetDemo(): Promise<void> {
  await reset();
  // The data's fresh, and so is this browser: pins, settings and list choices back to how a first visit has them, so
  // nothing kept here is saved back to the fresh account after the reload.
  clearAllAppStorage();
  closeHalfView();
  demoNavigation.assign(`${base()}/`);
}

/** Who's signed in to the demo, or null. */
export const demoUser = () => db().user;
