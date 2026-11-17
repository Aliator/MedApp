/** The notepad's own window: its name (so a second click finds it rather than opening another), size and address. */
export const NOTEPAD_WINDOW_NAME = 'medapp-notepad';
export const NOTEPAD_WINDOW_FEATURES = 'popup,width=420,height=640';
export const NOTEPAD_WINDOW_PATH = '/notepad';

/** Said when the browser refused to open the window. */
export const NOTEPAD_WINDOW_BLOCKED = "Your browser blocked the notepad's window. Allow pop-ups for MedApp, then try again.";

/** How long a window that wasn't closed gets before it goes to the start page instead (see `closeNotepadWindow`). */
const CLOSE_FALLBACK_MS = 300;

/** Whether this window is showing the notepad's page, on MedApp's own address (false for a new, blank window). */
function showsNotepad(popup: Window): boolean {
  try {
    return popup.location.origin === window.location.origin && popup.location.pathname === NOTEPAD_WINDOW_PATH;
  } catch {
    // Somewhere else entirely (another site's page, which can't be read).
    return false;
  }
}

/**
 * Opens the notepad in a small window of its own, or brings it forward if it's open already. Call it straight from a
 * click, so pop-up blockers allow it. Returns false when the browser blocked it.
 *
 * It opens an empty address first: that finds the window of that name as it is, without reloading it (a reload would
 * lose a jotting not saved yet). Only a new window (still blank), or one that has gone elsewhere, is sent to the
 * notepad's page.
 */
export function openNotepadWindow(): boolean {
  const popup = window.open('', NOTEPAD_WINDOW_NAME, NOTEPAD_WINDOW_FEATURES);
  if (!popup) return false;
  if (!showsNotepad(popup)) popup.location.replace(new URL(NOTEPAD_WINDOW_PATH, window.location.href).href);
  popup.focus();
  return true;
}

/**
 * Closes the notepad's window. A browser only lets a page close a window a page opened, so one opened another way
 * (the address typed into a tab) goes to the start page instead.
 */
export function closeNotepadWindow() {
  window.close();
  setTimeout(() => {
    if (!window.closed) window.location.assign('/');
  }, CLOSE_FALLBACK_MS);
}
