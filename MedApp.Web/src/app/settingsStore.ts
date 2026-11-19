import {createContext, useCallback, useContext, useSyncExternalStore} from 'react';

/**
 * The app's settings (colours, text size, list defaults, favourites, accessibility and the rest), one place for all
 * of them: each is a string under a `medapp.` key, kept in this browser (localStorage, so they're there before
 * sign-in) and, while signed in, with the account (`SettingsSync`), so they're the same on every computer.
 *
 * Settings are declared with `defineSetting`, which is what's saved with the account; providers read and write them
 * with `useAppSetting`, and re-render when they change, whether here or from the account.
 */

type Listener = () => void;

const keys = new Set<string>();
const values = new Map<string, string | null>();
const listeners = new Set<Listener>();
const localListeners = new Set<Listener>();

function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null; // Storage blocked: the setting lasts until the page reloads.
  }
}

function writeStorage(key: string, value: string | null) {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch {
    // Storage blocked: the setting lasts until the page reloads.
  }
}

const notify = (set: Set<Listener>) => set.forEach((listener) => listener());

/** The prefix of every key the app keeps in the browser's storage. */
export const STORAGE_PREFIX = 'medapp.';

/** Settings that stay in this browser after sign-out: purely how the app looks, so the sign-in page looks the same. */
const keptOnSignOut = new Set<string>();

/**
 * Declares a setting kept with the account. Returns its key, for the module to use. `keepOnSignOut` is only for
 * purely visual preferences (colours, text size, accessibility); everything else is cleared from this browser at
 * sign-out, and comes back from the account at the next sign-in.
 */
export function defineSetting(key: string, {keepOnSignOut = false}: {keepOnSignOut?: boolean} = {}): string {
  keys.add(key);
  if (keepOnSignOut) keptOnSignOut.add(key);
  return key;
}

/** The app's keys (`medapp.…`) in one of the browser's stores; none if it can't be read. */
function appKeys(storage: () => Storage): string[] {
  try {
    const store = storage();
    return Array.from({length: store.length}, (_, i) => store.key(i)).filter(
      (key): key is string => key !== null && key.startsWith(STORAGE_PREFIX),
    );
  } catch {
    return [];
  }
}

function removeFrom(storage: () => Storage, key: string) {
  try {
    storage().removeItem(key);
  } catch {
    // Storage blocked: there's nothing in it to clear.
  }
}

/**
 * Signing out on a shared computer leaves nothing of the user's behind in this browser (ASVS V14.3.1): every
 * `medapp.` key in localStorage and sessionStorage is removed (saved searches, pins, list and navigation preferences,
 * the half view), except the purely visual settings declared with `keepOnSignOut`. Settings in use go back to their
 * defaults at once, without being saved to the account, which still has them for the next sign-in.
 */
export function clearStorageAtSignOut() {
  clearStorage((key) => keptOnSignOut.has(key));
}

/**
 * Everything the app keeps in this browser removed, the visual settings too, and every setting back to its default at
 * once, without being saved: for the light demo's Reset, which starts the demo again from scratch (the owner,
 * 19 November 2026: pins, Shrink the side nav and the rest go back to how a first visit has them).
 */
export function clearAllAppStorage() {
  clearStorage(() => false);
}

/** Removes every `medapp.` key but those `keep` keeps, from both stores, and puts those settings back to their defaults. */
function clearStorage(keep: (key: string) => boolean) {
  for (const key of appKeys(() => localStorage)) if (!keep(key)) removeFrom(() => localStorage, key);
  for (const key of appKeys(() => sessionStorage)) removeFrom(() => sessionStorage, key);
  let isChanged = false;
  for (const key of keys) {
    if (keep(key) || values.get(key) === null) continue;
    values.set(key, null);
    isChanged = true;
  }
  if (isChanged) notify(listeners);
}

type Flusher = () => Promise<void>;

const flushers = new Set<Flusher>();

/**
 * Registers a save that may be pending (`SettingsSync`'s, a moment after the last change), to be made at once by
 * `flushSettings`. Returns a function that removes it.
 */
export function onFlushSettings(flusher: Flusher) {
  flushers.add(flusher);
  return () => {
    flushers.delete(flusher);
  };
}

/**
 * Makes any pending save of the settings to the account now, so a change made just before signing out (a pin, say)
 * isn't lost when signing out clears this browser. A failed save is ignored: signing out goes ahead.
 */
export async function flushSettings() {
  await Promise.all([...flushers].map((flush) => flush().catch(() => {})));
}

/** A setting changed in another tab of this browser shows here too (not saved again: that tab saves it). */
function onOtherTabChange(event: StorageEvent) {
  if (event.storageArea !== null && event.storageArea !== localStorage) return;
  if (event.key === null) {
    values.clear();
    notify(listeners);
  } else if (keys.has(event.key)) {
    values.set(event.key, event.newValue);
    notify(listeners);
  }
}

let isWatchingOtherTabs = false;

function watchOtherTabs() {
  if (isWatchingOtherTabs || typeof window === 'undefined') return;
  isWatchingOtherTabs = true;
  window.addEventListener('storage', onOtherTabChange);
}

export const appSettings = {
  get(key: string): string | null {
    if (!values.has(key)) values.set(key, readStorage(key));
    return values.get(key) ?? null;
  },

  /** A change made here: kept in this browser, and saved with the account (if signed in). */
  set(key: string, value: string | null) {
    if (appSettings.get(key) === value) return;
    values.set(key, value);
    writeStorage(key, value);
    notify(listeners);
    notify(localListeners);
  },

  subscribe(listener: Listener) {
    watchOtherTabs();
    listeners.add(listener);
    return () => listeners.delete(listener);
  },

  /** Called after each change made here (not one loaded from the account): what's worth saving. */
  onLocalChange(listener: Listener) {
    localListeners.add(listener);
    return () => localListeners.delete(listener);
  },

  /** Every declared setting that has a value, by key. */
  snapshot(): Record<string, string> {
    const out: Record<string, string> = {};
    for (const key of keys) {
      const value = appSettings.get(key);
      if (value !== null) out[key] = value;
    }
    return out;
  },

  /** The account's settings, replacing this browser's: a declared setting it doesn't have goes back to its default. */
  load(saved: Record<string, unknown>) {
    let isChanged = false;
    for (const key of keys) {
      const value = typeof saved[key] === 'string' ? (saved[key] as string) : null;
      if (appSettings.get(key) === value) continue;
      values.set(key, value);
      writeStorage(key, value);
      isChanged = true;
    }
    if (isChanged) notify(listeners);
  },

  /** For tests: forget what's been read, as storage is cleared between them. */
  reset() {
    values.clear();
  },
};

/**
 * The settings a page rendered on the server uses: the signed-in account's (this browser's aren't there to read),
 * by key. The browser's first render, which takes over the server's page (hydration), uses the same ones, so the
 * two match; after it, this browser's take over. Nothing outside server rendering provides it.
 */
export const ServerSettingsContext = createContext<Record<string, unknown> | null>(null);

/** A setting's value (null when it has none) and a way to change it; re-renders when it changes. */
export function useAppSetting(key: string): [string | null, (value: string | null) => void] {
  const server = useContext(ServerSettingsContext);
  const value = useSyncExternalStore(
    appSettings.subscribe,
    () => appSettings.get(key),
    () => (typeof server?.[key] === 'string' ? (server[key] as string) : null),
  );
  const set = useCallback((next: string | null) => appSettings.set(key, next), [key]);
  return [value, set];
}
