import {DEFAULT_PAGE_SIZE} from '../../app/listSettings';
import type {ListFilterFavourite, ListFilterField, ListFilterValues} from '../../components/tables/ListFilterBar';
import type {SetupListSearch} from '../listSearch';
import {saveProblem, type SaveSubject} from '../saveProblem';

/** Whether a setup list shows the ones in use, the archived ones, or both. */
export type SetupStatus = 'active' | 'archived' | 'any';
export const setupStatusOf = (search: SetupListSearch): SetupStatus => search.status ?? 'active';

const byName = (a: {name?: string | null}, b: {name?: string | null}) =>
  (a.name ?? '').localeCompare(b.name ?? '', undefined, {sensitivity: 'base'});

/**
 * One page of a setup list (stages or templates). These lists are short and come whole from the API, so they're
 * filtered, sorted (by name, or by `compare`) and paged here: active ones unless asked for archived ones.
 */
export function setupPage<T extends {name?: string | null; isArchived?: boolean}>(
  items: readonly T[],
  search: SetupListSearch,
  pageSize: number = DEFAULT_PAGE_SIZE,
  compare: (a: T, b: T) => number = byName,
) {
  const status = setupStatusOf(search);
  const name = search.name?.toLowerCase();
  const shown = items
    .filter((item) => status === 'any' || (status === 'archived') === (item.isArchived ?? false))
    .filter((item) => !name || (item.name ?? '').toLowerCase().includes(name))
    .sort(compare);
  const pages = Math.max(1, Math.ceil(shown.length / pageSize));
  const page = Math.min(search.page ?? 1, pages);
  return {
    rows: shown.slice((page - 1) * pageSize, page * pageSize),
    page,
    total: shown.length,
  };
}

/** A setup list's search fields: its name, and Active or Archived. */
export const SETUP_FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'name', label: 'Name'},
  {
    kind: 'choice',
    key: 'status',
    label: 'Status',
    options: [
      {value: 'active', label: 'Active'},
      {value: 'archived', label: 'Archived'},
    ],
  },
];

/** A setup list's quick look-ups: the ones in use, the archived ones, or both. */
export const SETUP_FAVOURITES: ListFilterFavourite[] = [
  {label: 'Active', values: {status: 'active'}},
  {label: 'Archived', values: {status: 'archived'}},
  {label: 'All', values: {}},
];

/**
 * The search bar's values from the URL, and back: the status filter shows as Active when the URL has none (the
 * default), and removing it is `any` (archived ones as well).
 */
export function setupBarValues(search: SetupListSearch): ListFilterValues {
  const status = setupStatusOf(search);
  return {name: search.name, status: status === 'any' ? undefined : status};
}

export function fromSetupBar(values: ListFilterValues): SetupListSearch {
  return {
    name: typeof values.name === 'string' ? values.name : undefined,
    status: values.status === undefined ? 'any' : values.status === 'archived' ? 'archived' : undefined,
  };
}

/** What a setup list's subtitle says it shows, e.g. "3 active stages", "1 matching template". */
export function setupSubtitle(total: number, search: SetupListSearch, one: string, many: string) {
  const noun = total === 1 ? one : many;
  if (search.name) return `${total} matching ${noun}`;
  const status = setupStatusOf(search);
  return `${total} ${status === 'any' ? '' : `${status} `}${noun}`;
}

/** A failed action's words for a banner's headline ("Site not archived"). */
const NOT_DONE = {
  save: 'not saved',
  add: 'not added',
  delete: 'not deleted',
  archive: 'not archived',
  restore: 'not restored',
} as const;

/** Why an action failed, for a page's header: a headline naming it ("Site not archived"), and the message behind the "i". */
export type PageError = {label: string; message: string} | null;

/** A failed action as a `PageError` (`saveProblem`'s message, with a headline naming the action); null for a 400, shown on the form's fields. */
export function actionProblem(error: unknown, action: keyof typeof NOT_DONE, subject: SaveSubject): PageError {
  const {message} = saveProblem(error, action, subject);
  return message ? {label: `${subject.charAt(0).toUpperCase()}${subject.slice(1)} ${NOT_DONE[action]}`, message} : null;
}
