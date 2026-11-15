import {useCallback, useEffect, useState} from 'react';

/** How many of each kind (patients, procedures) one user can pin in the side nav. */
export const MAX_PINS = 5;

export type PinKind = 'patients' | 'procedures';

/**
 * Pins are kept in this browser, per user and kind, so staff sharing a computer each have their own. Only IDs
 * are stored, never names or other details; the side nav looks those up from the API.
 */
export const pinsKey = (kind: PinKind, username: string) =>
  `medapp.${kind === 'patients' ? 'pinnedPatients' : 'pinnedProcedures'}.${username}`;

const isId = (value: unknown): value is string => typeof value === 'string' && value.length > 0;

function read(key: string): string[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(key) ?? '[]');
    return Array.isArray(parsed) ? [...new Set(parsed.filter(isId))].slice(0, MAX_PINS) : [];
  } catch {
    return [];
  }
}

function write(key: string, ids: string[]) {
  try {
    localStorage.setItem(key, JSON.stringify(ids));
  } catch {
    // Storage can be unavailable (private browsing, blocked site data): the pins then last for this visit only.
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
 * The signed-in user's pinned patients or procedures: up to MAX_PINS of each, kept until unpinned, and in step
 * across this browser's tabs.
 */
export function usePins(kind: PinKind, username: string): Pins {
  const key = pinsKey(kind, username);
  // The pins are held with the key they belong to, so a different user signing in on this browser reads their own.
  const [held, setHeld] = useState(() => ({key, ids: read(key)}));
  const ids = held.key === key ? held.ids : read(key);

  // Pinning in another tab shows here too.
  useEffect(() => {
    const onStorage = (event: StorageEvent) => {
      if (event.key === key) setHeld({key, ids: read(key)});
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, [key]);

  const update = useCallback(
    (change: (current: string[]) => string[]) =>
      setHeld((current) => {
        const next = change(current.key === key ? current.ids : read(key));
        write(key, next);
        return {key, ids: next};
      }),
    [key],
  );

  return {
    ids,
    isPinned: (id) => ids.includes(id),
    isFull: ids.length >= MAX_PINS,
    pin: (id) => update((current) => (current.includes(id) || current.length >= MAX_PINS ? current : [...current, id])),
    unpin: (id) => update((current) => current.filter((pinned) => pinned !== id)),
  };
}
