import {useCallback, useSyncExternalStore} from 'react';

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

/** Declares a setting kept with the account. Returns its key, for the module to use. */
export function defineSetting(key: string): string {
  keys.add(key);
  return key;
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

/** A setting's value (null when it has none) and a way to change it; re-renders when it changes. */
export function useAppSetting(key: string): [string | null, (value: string | null) => void] {
  const value = useSyncExternalStore(
    appSettings.subscribe,
    () => appSettings.get(key),
    () => null,
  );
  const set = useCallback((next: string | null) => appSettings.set(key, next), [key]);
  return [value, set];
}
