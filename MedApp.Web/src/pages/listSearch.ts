/**
 * The list pages keep their state in the URL, as the Blazor pages do, so a search or page is a shareable link:
 * `page`, `sort`, `asc`, and the search filters. These parse what's in the address bar into typed values,
 * dropping anything invalid, so a bad link shows the default list rather than an error.
 */

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

function text(value: unknown): string | undefined {
  if (typeof value !== 'string' && typeof value !== 'number') return undefined;
  const trimmed = String(value).trim();
  return trimmed ? trimmed : undefined;
}

function page(value: unknown): number | undefined {
  const n = typeof value === 'number' ? value : Number(text(value));
  return Number.isInteger(n) && n > 1 ? n : undefined;
}

function bool(value: unknown): boolean | undefined {
  if (typeof value === 'boolean') return value;
  const t = text(value)?.toLowerCase();
  return t === 'true' ? true : t === 'false' ? false : undefined;
}

function date(value: unknown): string | undefined {
  const t = text(value);
  return t && ISO_DATE.test(t) ? t : undefined;
}

function oneOf<T extends string>(value: unknown, options: readonly T[]): T | undefined {
  const t = text(value);
  return options.find((o) => o.toLowerCase() === t?.toLowerCase());
}

/** Undefined entries are left out, so the URL only shows what's set. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as T;
}

// Patients ------------------------------------------------------------------------------------------------

/** URL sort names, as in Blazor (so old links keep working), and the API's names for them. */
export const PATIENT_SORTS = {Patients: 'name', DateOfBirth: 'dateOfBirth', Email: 'email'} as const;
export type PatientSortColumn = keyof typeof PATIENT_SORTS;

export interface PatientsListSearch {
  page?: number;
  sort?: PatientSortColumn;
  asc?: boolean;
  first?: string;
  last?: string;
  email?: string;
  dobFrom?: string;
  dobTo?: string;
}

export function parsePatientsSearch(raw: Record<string, unknown>): PatientsListSearch {
  return compact({
    page: page(raw.page),
    sort: oneOf(raw.sort, Object.keys(PATIENT_SORTS) as PatientSortColumn[]),
    asc: bool(raw.asc),
    first: text(raw.first),
    last: text(raw.last),
    email: text(raw.email),
    dobFrom: date(raw.dobFrom),
    dobTo: date(raw.dobTo),
  });
}

export const hasPatientFilters = (s: PatientsListSearch) => Boolean(s.first || s.last || s.email || s.dobFrom || s.dobTo);

// Users ---------------------------------------------------------------------------------------------------

export interface UsersListSearch {
  page?: number;
  sort?: 'User';
  asc?: boolean;
  username?: string;
  role?: string;
}

export function parseUsersSearch(raw: Record<string, unknown>): UsersListSearch {
  return compact({
    page: page(raw.page),
    sort: oneOf(raw.sort, ['User'] as const),
    asc: bool(raw.asc),
    username: text(raw.username),
    role: text(raw.role),
  });
}

export const hasUserFilters = (s: UsersListSearch) => Boolean(s.username || s.role);

// Home's task lists ------------------------------------------------------------------------------------

/** URL sort names for a task list, and the API's names for them. Due date, soonest first, by default. */
export const TASK_SORTS = {Procedure: 'title', Due: 'dueDate', Priority: 'priority'} as const;
export type TaskSortColumn = keyof typeof TASK_SORTS;

export const TASK_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;
export type TaskPriority = (typeof TASK_PRIORITIES)[number];

/** One task list's page, sort and filters. */
export interface TaskListSearch {
  page?: number;
  sort?: TaskSortColumn;
  asc?: boolean;
  /** Matches the procedure title, template or patient name. */
  q?: string;
  priority?: TaskPriority;
  overdue?: boolean;
  dueFrom?: string;
  dueTo?: string;
}

/** Home's two lists: procedures nobody has started on yet, and those under way. */
export type TaskListKind = 'notStarted' | 'started';

type Prefixed<P extends string, T> = {[K in keyof T & string as `${P}${Capitalize<K>}`]?: T[K]};

/**
 * Home's URL state. Both lists keep their own page, sort and filters, under a prefix (`notStartedPage`,
 * `startedSort`, ...). `show` is which list a narrow window shows; the not started one by default.
 */
export type HomeSearch = {show?: 'started'} & Prefixed<'notStarted', TaskListSearch> & Prefixed<'started', TaskListSearch>;

const TASK_KEYS = ['page', 'sort', 'asc', 'q', 'priority', 'overdue', 'dueFrom', 'dueTo'] as const satisfies readonly (keyof TaskListSearch)[];

const prefixedKey = (kind: TaskListKind, key: string) => `${kind}${key.charAt(0).toUpperCase()}${key.slice(1)}`;

function parseTaskList(raw: Record<string, unknown>, kind: TaskListKind): TaskListSearch {
  const get = (key: keyof TaskListSearch) => raw[prefixedKey(kind, key)];
  return compact({
    page: page(get('page')),
    sort: oneOf(get('sort'), Object.keys(TASK_SORTS) as TaskSortColumn[]),
    asc: bool(get('asc')),
    q: text(get('q')),
    priority: oneOf(get('priority'), TASK_PRIORITIES),
    overdue: bool(get('overdue')) ? true : undefined,
    dueFrom: date(get('dueFrom')),
    dueTo: date(get('dueTo')),
  });
}

/** The URL keys for one list's search. */
function toPrefixed(kind: TaskListKind, list: TaskListSearch): Record<string, unknown> {
  return Object.fromEntries(Object.entries(compact(list)).map(([key, value]) => [prefixedKey(kind, key), value]));
}

export function parseHomeSearch(raw: Record<string, unknown>): HomeSearch {
  return compact({
    show: oneOf(raw.show, ['started'] as const),
    ...toPrefixed('notStarted', parseTaskList(raw, 'notStarted')),
    ...toPrefixed('started', parseTaskList(raw, 'started')),
  }) as HomeSearch;
}

/** One list's search, out of Home's URL state. */
export function taskListSearch(home: HomeSearch, kind: TaskListKind): TaskListSearch {
  return parseTaskList(home as Record<string, unknown>, kind);
}

/** Home's URL state with one list's search replaced, leaving the other list as it was. */
export function withTaskListSearch(home: HomeSearch, kind: TaskListKind, list: TaskListSearch): HomeSearch {
  const others = Object.fromEntries(
    Object.entries(home).filter(([key]) => !TASK_KEYS.some((k) => key === prefixedKey(kind, k))),
  );
  return compact({...others, ...toPrefixed(kind, list)}) as HomeSearch;
}

export const hasTaskFilters = (s: TaskListSearch) => Boolean(s.q || s.priority || s.overdue || s.dueFrom || s.dueTo);

// Procedures --------------------------------------------------------------------------------------------

/** URL sort names for the procedure lists, and the API's names for them. Due date, soonest first, by default. */
export const PROCEDURE_SORTS = {Procedure: 'title', Status: 'status', Due: 'dueDate', Priority: 'priority', Updated: 'lastUpdated'} as const;
export type ProcedureSortColumn = keyof typeof PROCEDURE_SORTS;

export const PROCEDURE_STATUSES = ['Unassigned', 'Assigned', 'InProgress', 'InReview', 'Blocked', 'Completed', 'Cancelled', 'Archived'] as const;
export type ProcedureStatus = (typeof PROCEDURE_STATUSES)[number];

/** The all-procedures list and a patient's procedures: page, sort and filters. */
export interface ProceduresListSearch {
  page?: number;
  sort?: ProcedureSortColumn;
  asc?: boolean;
  /** Matches the title, template or patient name. */
  q?: string;
  /** Only this status. Choosing a closed one shows it even without `closed`. */
  status?: ProcedureStatus;
  priority?: TaskPriority;
  overdue?: boolean;
  /** Only procedures assigned to the signed-in user. */
  mine?: boolean;
  /** Include Completed, Cancelled and Archived (hidden by default). */
  closed?: boolean;
  dueFrom?: string;
  dueTo?: string;
}

const flag = (value: unknown) => (bool(value) ? true : undefined);

export function parseProceduresSearch(raw: Record<string, unknown>): ProceduresListSearch {
  return compact({
    page: page(raw.page),
    sort: oneOf(raw.sort, Object.keys(PROCEDURE_SORTS) as ProcedureSortColumn[]),
    asc: bool(raw.asc),
    q: text(raw.q),
    status: oneOf(raw.status, PROCEDURE_STATUSES),
    priority: oneOf(raw.priority, TASK_PRIORITIES),
    overdue: flag(raw.overdue),
    mine: flag(raw.mine),
    closed: flag(raw.closed),
    dueFrom: date(raw.dueFrom),
    dueTo: date(raw.dueTo),
  });
}

export const hasProcedureFilters = (s: ProceduresListSearch) =>
  Boolean(s.q || s.status || s.priority || s.overdue || s.mine || s.closed || s.dueFrom || s.dueTo);
