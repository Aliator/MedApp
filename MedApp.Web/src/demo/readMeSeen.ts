/**
 * Whether this browser has seen the light demo's Read Me, so it's shown once, on the first visit (`DemoBanner`). Kept
 * in `localStorage`, outside the app's own `medapp.` keys, so signing out doesn't clear it. Storage may be blocked: then
 * it shows each time the app loads, which is harmless.
 */
const KEY = 'medappLightDemo.readMeSeen';

/** The Read Me's address in the app. */
export const READ_ME_PATH = '/read-me';

export function hasSeenReadMe(): boolean {
  try {
    return localStorage.getItem(KEY) === 'true';
  } catch {
    return false;
  }
}

export function markReadMeSeen(): void {
  try {
    localStorage.setItem(KEY, 'true');
  } catch {
    // Not kept: it shows again next time.
  }
}
