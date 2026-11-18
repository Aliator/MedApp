import {useCallback, useEffect, useMemo} from 'react';
import {pinsNow} from '../../app/beforePathways';
import {appSettings, defineSetting, useAppSetting} from '../../app/settingsStore';

/** How many of each kind (patients, pathways) one user can pin in the side nav. */
export const MAX_PINS = 5;

export type PinKind = 'patients' | 'pathways';

/**
 * Pins are one of the account's settings (`settingsStore`), so they follow the user between browsers, and signing out
 * clears them from this one: the next person on a shared computer doesn't see them. Only IDs are kept, never names
 * or other details; the side nav looks those up from the API.
 */
export const PINS_KEY = defineSetting('medapp.pins');

/**
 * Where this browser kept a user's pins before they moved to the account (the key named the user). Anything found
 * there is carried over to the account once; signing out removes it.
 */
export const legacyPinsKey = (kind: PinKind, username: string) =>
  `medapp.${kind === 'patients' ? 'pinnedPatients' : 'pinnedProcedures'}.${username}`;

type AllPins = Record<PinKind, string[]>;

const isId = (value: unknown): value is string => typeof value === 'string' && value.length > 0;

/** A list of IDs, without repeats or anything that isn't one, and no more than MAX_PINS. */
function ids(value: unknown): string[] {
  return Array.isArray(value) ? [...new Set(value.filter(isId))].slice(0, MAX_PINS) : [];
}

function parse(stored: string | null): AllPins {
  try {
    // Read by the names used now: pins saved before pathways were renamed keep them under their old name.
    const parsed = pinsNow(JSON.parse(stored ?? '{}'));
    return {patients: ids(parsed?.patients), pathways: ids(parsed?.pathways)};
  } catch {
    return {patients: [], pathways: []};
  }
}

function readLegacy(kind: PinKind, username: string): string[] {
  try {
    return ids(JSON.parse(localStorage.getItem(legacyPinsKey(kind, username)) ?? '[]'));
  } catch {
    return [];
  }
}

export interface Pins {
  /** Pinned IDs, in the order they were pinned. */
  ids: string[];
  isPinned: (id: string) => boolean;
  /** Whether the limit is reached, so no more can be pinned. */
  isFull: boolean;
  pin: (id: string) => void;
  unpin: (id: string) => void;
}

/**
 * The signed-in user's pinned patients or pathways: up to MAX_PINS of each, kept until unpinned, and in step
 * across this browser's tabs. `username` is only for finding pins this browser kept before they moved to the account.
 */
export function usePins(kind: PinKind, username: string): Pins {
  const [stored, store] = useAppSetting(PINS_KEY);
  const all = useMemo(() => parse(stored), [stored]);
  const current = all[kind];

  // Pins this browser kept for the user before they were part of the account: carried over, the first time.
  useEffect(() => {
    if (stored !== null) return;
    const legacy = {patients: readLegacy('patients', username), pathways: readLegacy('pathways', username)};
    if (legacy.patients.length || legacy.pathways.length) store(JSON.stringify(legacy));
  }, [stored, store, username]);

  // From the latest pins, not this render's, so several changes in a row (dropping gone ones) all count.
  const update = useCallback(
    (change: (current: string[]) => string[]) => {
      const latest = parse(appSettings.get(PINS_KEY));
      store(JSON.stringify({...latest, [kind]: change(latest[kind])}));
    },
    [kind, store],
  );

  return {
    ids: current,
    isPinned: (id) => current.includes(id),
    isFull: current.length >= MAX_PINS,
    pin: (id) => update((pinned) => (pinned.includes(id) || pinned.length >= MAX_PINS ? pinned : [...pinned, id])),
    unpin: (id) => update((pinned) => pinned.filter((p) => p !== id)),
  };
}
