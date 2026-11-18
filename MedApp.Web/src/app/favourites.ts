import {useCallback, useMemo} from 'react';
import {defineSetting, useAppSetting} from './settingsStore';

/** A list's search, by URL key (as `ListFilterBar` reads and writes it). */
export type FavouriteValues = Record<string, string | boolean | undefined>;

/** One of a list's own tabs (its defaults): a name, and the search it shows. */
export interface DefaultTab {
  label: string;
  values: FavouriteValues;
}

/** A tab the list shows: one of its defaults (as the person edited it, if they did) or one the person added. */
export interface ListTab {
  id: string;
  label: string;
  values: FavouriteValues;
  isDefault: boolean;
}

/** A tab the person added. */
interface AddedTab {
  id: string;
  name: string;
  values: FavouriteValues;
}

/**
 * Only what the person changed about one list's tabs, so a list they never configured always shows its current
 * defaults, and resetting it removes the lot: the tabs they added, the defaults they edited or removed (by ID), and the
 * order (IDs; tabs it doesn't name follow, in their own order).
 */
export interface TabChanges {
  added: AddedTab[];
  edited: Record<string, {name: string; values: FavouriteValues}>;
  removed: string[];
  order?: string[];
}

/**
 * Every list's changes, by list (e.g. `patients`), kept in this browser and with the account (`settingsStore`). The
 * key is the one saved searches were kept under, so they carry over (`parseTabChanges`).
 */
const KEY = defineSetting('medapp.favourites');

/** A default tab's ID: its own label, which the code gives it. */
export const defaultTabId = (label: string) => `default:${label}`;

const isValues = (value: unknown): value is FavouriteValues =>
  typeof value === 'object' &&
  value !== null &&
  !Array.isArray(value) &&
  Object.values(value).every((v) => v === undefined || typeof v === 'string' || typeof v === 'boolean');

const isAdded = (f: unknown): f is AddedTab => {
  const tab = f as AddedTab | null;
  return typeof tab?.id === 'string' && typeof tab?.name === 'string' && isValues(tab?.values);
};

const strings = (value: unknown) => (Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : []);

/**
 * One list's stored changes. A list stored before tabs (`{saved, startId}`: the searches saved from the star, and the
 * one the list started on) becomes tabs the person added, after the defaults, with the starting one first, so the
 * list still opens on it.
 */
function parseList(value: unknown): TabChanges | undefined {
  if (!value || typeof value !== 'object') return undefined;
  const entry = value as Record<string, unknown>;
  if ('saved' in entry && entry.added === undefined) {
    const added = Array.isArray(entry.saved) ? entry.saved.filter(isAdded) : [];
    const startId = typeof entry.startId === 'string' && added.some((f) => f.id === entry.startId) ? entry.startId : undefined;
    return added.length ? {added, edited: {}, removed: [], order: startId ? [startId] : undefined} : undefined;
  }
  const edited: TabChanges['edited'] = {};
  if (entry.edited && typeof entry.edited === 'object')
    for (const [id, edit] of Object.entries(entry.edited as Record<string, unknown>)) {
      const e = edit as {name?: unknown; values?: unknown} | null;
      if (typeof e?.name === 'string' && isValues(e?.values)) edited[id] = {name: e.name, values: e.values};
    }
  return {
    added: Array.isArray(entry.added) ? entry.added.filter(isAdded) : [],
    edited,
    removed: strings(entry.removed),
    order: Array.isArray(entry.order) ? strings(entry.order) : undefined,
  };
}

/** Every list's changes, from what's stored; anything it can't read is left out. */
export function parseTabChanges(stored: string | null): Record<string, TabChanges> {
  try {
    const parsed: unknown = JSON.parse(stored ?? '{}');
    if (!parsed || typeof parsed !== 'object') return {};
    const lists: Record<string, TabChanges> = {};
    for (const [list, value] of Object.entries(parsed as Record<string, unknown>)) {
      const changes = parseList(value);
      if (changes) lists[list] = changes;
    }
    return lists;
  } catch {
    return {};
  }
}

/** The tabs a list shows: its defaults with the person's changes, then the ones they added, in their order. */
export function resolveTabs(defaults: readonly DefaultTab[], changes: TabChanges | undefined): ListTab[] {
  const tabs: ListTab[] = defaults
    .map((d) => ({id: defaultTabId(d.label), label: d.label, values: d.values, isDefault: true}))
    .filter((t) => !changes?.removed.includes(t.id))
    .map((t) => {
      const edit = changes?.edited[t.id];
      return edit ? {...t, label: edit.name, values: edit.values} : t;
    });
  for (const added of changes?.added ?? []) tabs.push({id: added.id, label: added.name, values: added.values, isDefault: false});
  const order = changes?.order;
  if (!order?.length) return tabs;
  const rank = (t: ListTab) => {
    const i = order.indexOf(t.id);
    return i < 0 ? order.length : i;
  };
  // Stable: tabs the order doesn't name keep their own order, after those it does.
  return tabs
    .map((t, i) => ({t, i}))
    .sort((a, b) => rank(a.t) - rank(b.t) || a.i - b.i)
    .map(({t}) => t);
}

const newId = () =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;

const NO_CHANGES: TabChanges = {added: [], edited: {}, removed: []};

/**
 * One list's tabs (`ListFilterBar`): its defaults, with what the person changed (add, edit, remove, reorder), and a
 * way back to the defaults. Only the changes are stored.
 */
export function useListTabs(list: string, defaults: readonly DefaultTab[]) {
  const [stored, store] = useAppSetting(KEY);
  const all = useMemo(() => parseTabChanges(stored), [stored]);
  const changes = all[list];
  const tabs = useMemo(() => resolveTabs(defaults, changes), [defaults, changes]);

  const update = useCallback(
    (next: TabChanges | undefined) => {
      const lists = {...all};
      if (next) lists[list] = next;
      else delete lists[list];
      store(Object.keys(lists).length ? JSON.stringify(lists) : null);
    },
    [all, list, store],
  );
  const current = changes ?? NO_CHANGES;
  const isDefault = (id: string) => tabs.find((t) => t.id === id)?.isDefault === true;

  return {
    tabs,
    /** Whether the person has changed this list's tabs (so Reset has something to undo). */
    isCustomised: changes !== undefined,
    add: (name: string, values: FavouriteValues) => {
      const tab = {id: newId(), name: name.trim(), values};
      update({...current, added: [...current.added, tab], order: current.order ? [...current.order, tab.id] : undefined});
      return tab.id;
    },
    edit: (id: string, name: string, values: FavouriteValues) =>
      update(
        isDefault(id)
          ? {...current, edited: {...current.edited, [id]: {name: name.trim(), values}}}
          : {...current, added: current.added.map((t) => (t.id === id ? {...t, name: name.trim(), values} : t))},
      ),
    remove: (id: string) => {
      const edited = {...current.edited};
      delete edited[id];
      update({
        added: current.added.filter((t) => t.id !== id),
        edited,
        removed: isDefault(id) ? [...current.removed, id] : current.removed,
        order: current.order?.filter((o) => o !== id),
      });
    },
    move: (id: string, to: number) => {
      const ids = tabs.map((t) => t.id);
      const from = ids.indexOf(id);
      if (from < 0) return;
      ids.splice(from, 1);
      ids.splice(Math.max(0, Math.min(to, ids.length)), 0, id);
      update({...current, order: ids});
    },
    reset: () => update(undefined),
  };
}
