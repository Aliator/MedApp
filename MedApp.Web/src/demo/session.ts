import {READ_ME_PATH} from './readMeSeen';
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

/** Keeps the Read Me in the half view, on the right, through a full load (`AppLayout` reopens what's kept for the tab). */
function keepReadMeOnRight() {
  try {
    sessionStorage.setItem('medapp.halfView', READ_ME_PATH);
  } catch {
    // Without storage, the page opens on its own.
  }
}

/**
 * Signs in as another demo account and opens `path` (in the app, such as `/calendar`), with a full load, so every
 * cached answer is read again as them. With `readMeOnRight`, the page opens on the left with the Read Me beside it on
 * the right, as the tour's buttons open pages; otherwise on its own.
 */
export async function switchAccount(username: string, path = '/', {readMeOnRight = false}: {readMeOnRight?: boolean} = {}): Promise<void> {
  db().user = username;
  await save();
  if (readMeOnRight && path !== READ_ME_PATH) keepReadMeOnRight();
  else closeHalfView();
  demoNavigation.assign(`${base()}${path}`);
}

/** Reset: clears the demo's data, seeds it again (keeping who's signed in) and reloads at the start page. */
export async function resetDemo(): Promise<void> {
  await reset();
  closeHalfView();
  demoNavigation.assign(`${base()}/`);
}

/** Who's signed in to the demo, or null. */
export const demoUser = () => db().user;
