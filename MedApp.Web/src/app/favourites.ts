import {useCallback, useMemo} from 'react';
import {defineSetting, useAppSetting} from './settingsStore';

/** A list's search, by URL key (as `ListFilterBar` reads and writes it). */
export type FavouriteValues = Record<string, string | boolean | undefined>;

/** A search someone saved, to come back to from the star beside a list's search. */
export interface SavedFavourite {
  id: string;
  name: string;
  values: FavouriteValues;
}

/** One list's saved searches, and which (if any) it starts on. */
interface ListFavourites {
  saved: SavedFavourite[];
  startId?: string;
}

/** Every list's, by list (e.g. `patients`), kept in this browser and with the account (`settingsStore`). */
const KEY = defineSetting('medapp.favourites');

const isValues = (value: unknown): value is FavouriteValues =>
  typeof value === 'object' &&
  value !== null &&
  Object.values(value).every((v) => v === undefined || typeof v === 'string' || typeof v === 'boolean');

function parse(stored: string | null): Record<string, ListFavourites> {
  try {
    const parsed: unknown = JSON.parse(stored ?? '{}');
    if (!parsed || typeof parsed !== 'object') return {};
    const lists: Record<string, ListFavourites> = {};
    for (const [list, value] of Object.entries(parsed as Record<string, unknown>)) {
      const entry = value as {saved?: unknown; startId?: unknown};
      const saved = Array.isArray(entry?.saved)
        ? entry.saved.filter(
            (f): f is SavedFavourite =>
              typeof f?.id === 'string' && typeof f?.name === 'string' && isValues(f?.values),
          )
        : [];
      const startId = typeof entry?.startId === 'string' && saved.some((f) => f.id === entry.startId) ? entry.startId : undefined;
      lists[list] = {saved, startId};
    }
    return lists;
  } catch {
    return {};
  }
}

const newId = () =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;

/** One list's saved searches, with ways to add, rename and delete them and choose the one it starts on. */
export function useSavedFavourites(list: string) {
  const [stored, store] = useAppSetting(KEY);
  const all = useMemo(() => parse(stored), [stored]);
  const current = all[list] ?? {saved: []};

  const update = useCallback(
    (next: ListFavourites) => store(JSON.stringify({...all, [list]: next})),
    [all, list, store],
  );

  return {
    saved: current.saved,
    start: current.saved.find((f) => f.id === current.startId),
    add: (name: string, values: FavouriteValues, isStart: boolean) => {
      const favourite = {id: newId(), name: name.trim(), values};
      update({saved: [...current.saved, favourite], startId: isStart ? favourite.id : current.startId});
    },
    rename: (id: string, name: string) =>
      update({...current, saved: current.saved.map((f) => (f.id === id ? {...f, name} : f))}),
    remove: (id: string) =>
      update({saved: current.saved.filter((f) => f.id !== id), startId: current.startId === id ? undefined : current.startId}),
    setStart: (id: string | undefined) => update({...current, startId: id}),
  };
}
