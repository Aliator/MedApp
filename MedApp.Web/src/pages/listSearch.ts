/**
 * The list pages keep their state in the URL, as the Blazor pages do, so a search or page is a shareable link:
 * `page`, `sort`, `asc`, and the search filters. These parse what's in the address bar into typed values,
 * dropping anything invalid, so a bad link shows the default list rather than an error.
 */

import {AUDIT_ACTIONS} from '../api/audit';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

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
  /** A choice (see `parseChoice`), of the roles there are. */
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

export const PROCEDURE_STATUSES = ['Unassigned', 'Assigned', 'InProgress', 'InReview', 'Blocked', 'Completed', 'Cancelled', 'Archived'] as const;
export type ProcedureStatus = (typeof PROCEDURE_STATUSES)[number];
/** The statuses of a procedure still being worked on: the ones lists show unless asked for closed ones too. */
export const OPEN_PROCEDURE_STATUSES = ['Unassigned', 'Assigned', 'InProgress', 'InReview', 'Blocked'] as const satisfies readonly ProcedureStatus[];

// Choices ---------------------------------------------------------------------------------------------------

/**
 * A choice filter's URL value (a list's power search, `ListFilterBar`): one value, a comma list ("is any of"), or a
 * comma list after `!` ("is not any of"). Checked against the options; anything else is dropped.
 */
export function parseChoice(value: unknown, options: readonly string[]): string | undefined {
  const t = text(value);
  if (!t) return undefined;
  const isNone = t.startsWith('!');
  const listed = (isNone ? t.slice(1) : t)
    .split(',')
    .map((v) => options.find((o) => o.toLowerCase() === v.trim().toLowerCase()))
    .filter((v): v is string => v !== undefined);
  return listed.length ? `${isNone ? '!' : ''}${[...new Set(listed)].join(',')}` : undefined;
}

/**
 * The values a choice filter means, for the API: those listed, or (after `!`) every one of `options` but those.
 * Undefined when there's no filter, or it leaves none.
 */
export function choiceValues(value: string | undefined, options: readonly string[]): string[] | undefined {
  if (!value) return undefined;
  const isNone = value.startsWith('!');
  const listed = (isNone ? value.slice(1) : value).split(',');
  const values = isNone ? options.filter((o) => !listed.includes(o)) : listed.filter((v) => options.includes(v));
  return values.length ? values : undefined;
}

// My Tasks ------------------------------------------------------------------------------------------------

/** URL sort names for My Tasks, and the API's names for them. Due date, soonest first, by default. */
export const TASK_SORTS = {Procedure: 'title', Due: 'dueDate', Priority: 'priority'} as const;
export type TaskSortColumn = keyof typeof TASK_SORTS;

export const TASK_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;
export type TaskPriority = (typeof TASK_PRIORITIES)[number];

/**
 * My Tasks' page, sort and filters. `progress` is Not started unless the URL says otherwise; `any` (its filter
 * removed) shows both.
 */
export interface TasksSearch {
  page?: number;
  sort?: TaskSortColumn;
  asc?: boolean;
  /** The procedure's title contains this. */
  title?: string;
  template?: string;
  patient?: string;
  progress?: 'started' | 'any';
  /** A choice: one, a comma list, or `!` and a comma list (see `parseChoice`). */
  status?: string;
  priority?: string;
  overdue?: boolean;
  dueFrom?: string;
  dueTo?: string;
}

export function parseTasksSearch(raw: Record<string, unknown>): TasksSearch {
  return compact({
    page: page(raw.page),
    sort: oneOf(raw.sort, Object.keys(TASK_SORTS) as TaskSortColumn[]),
    asc: bool(raw.asc),
    title: text(raw.title),
    template: text(raw.template),
    patient: text(raw.patient),
    progress: oneOf(raw.progress, ['started', 'any'] as const),
    status: parseChoice(raw.status, OPEN_PROCEDURE_STATUSES),
    priority: parseChoice(raw.priority, TASK_PRIORITIES),
    overdue: bool(raw.overdue) ? true : undefined,
    dueFrom: date(raw.dueFrom),
    dueTo: date(raw.dueTo),
  });
}

/** Filters beyond the progress (which always applies, Not started by default). */
export const hasTaskFilters = (s: TasksSearch) =>
  Boolean(s.title || s.template || s.patient || s.status || s.priority || s.overdue || s.dueFrom || s.dueTo);

// Procedures --------------------------------------------------------------------------------------------

/** URL sort names for the procedure lists, and the API's names for them. Due date, soonest first, by default. */
export const PROCEDURE_SORTS = {Procedure: 'title', Status: 'status', Due: 'dueDate', Priority: 'priority', Updated: 'lastUpdated'} as const;
export type ProcedureSortColumn = keyof typeof PROCEDURE_SORTS;

/** The all-procedures list and a patient's procedures: page, sort and filters. */
export interface ProceduresListSearch {
  page?: number;
  sort?: ProcedureSortColumn;
  asc?: boolean;
  /** The procedure's title contains this. */
  title?: string;
  template?: string;
  patient?: string;
  /** A choice (see `parseChoice`). Choosing closed statuses shows them even without `closed`. */
  status?: string;
  /** A choice (see `parseChoice`). */
  priority?: string;
  overdue?: boolean;
  /** Only procedures assigned to the signed-in user. */
  /** Only procedures assigned to the signed-in user. `false` (in the URL) overrides starting on them (Account Settings). */
  mine?: boolean;
  /** Include Completed, Cancelled and Archived (hidden by default). */
  closed?: boolean;
  /** Who it's assigned to: a user's ID, or `none` for nobody (e.g. from Insights' overdue work by assignee). */
  assignee?: string;
  dueFrom?: string;
  dueTo?: string;
}

const flag = (value: unknown) => (bool(value) ? true : undefined);

/** The `assignee` filter's value for nobody assigned. */
export const NOBODY = 'none';

/** An assignee filter: a user's ID, or `none`; anything else is dropped. */
function assignee(value: unknown): string | undefined {
  const t = text(value)?.toLowerCase();
  return t === NOBODY || (t && GUID.test(t)) ? t : undefined;
}

/**
 * The statuses a procedure list's search asks the API for: those chosen; for "not any of", the others (the open
 * ones only, unless closed ones are included too). Undefined for none chosen (the API's default: open ones).
 */
export function procedureStatuses(search: {status?: string; closed?: boolean}): string[] | undefined {
  const pool = search.status?.startsWith('!') && !search.closed ? OPEN_PROCEDURE_STATUSES : PROCEDURE_STATUSES;
  return choiceValues(search.status, pool);
}

export function parseProceduresSearch(raw: Record<string, unknown>): ProceduresListSearch {
  return compact({
    page: page(raw.page),
    sort: oneOf(raw.sort, Object.keys(PROCEDURE_SORTS) as ProcedureSortColumn[]),
    asc: bool(raw.asc),
    title: text(raw.title),
    template: text(raw.template),
    patient: text(raw.patient),
    status: parseChoice(raw.status, PROCEDURE_STATUSES),
    priority: parseChoice(raw.priority, TASK_PRIORITIES),
    overdue: flag(raw.overdue),
    mine: bool(raw.mine),
    closed: flag(raw.closed),
    assignee: assignee(raw.assignee),
    dueFrom: date(raw.dueFrom),
    dueTo: date(raw.dueTo),
  });
}

export const hasProcedureFilters = (s: ProceduresListSearch) =>
  Boolean(
    s.title || s.template || s.patient || s.status || s.priority || s.overdue || s.mine || s.closed || s.assignee || s.dueFrom || s.dueTo,
  );

// Procedure setup (admin): stage definitions and templates --------------------------------------------

/**
 * The stages and templates lists: the page and filters. `status` is Active unless the URL says otherwise; `any` (its
 * filter removed) shows archived ones too.
 */
export interface SetupListSearch {
  page?: number;
  /** The name contains this. */
  name?: string;
  status?: 'archived' | 'any';
}

export function parseSetupSearch(raw: Record<string, unknown>): SetupListSearch {
  return compact({
    page: page(raw.page),
    name: text(raw.name),
    // `archived=true` was the old "show archived" switch: archived ones as well.
    status: oneOf(raw.status, ['archived', 'any'] as const) ?? (flag(raw.archived) ? 'any' : undefined),
  });
}

// The audit log (admin) -------------------------------------------------------------------------------


/** The audit log: page, order (newest first unless `asc`) and filters. */
export interface AuditListSearch {
  page?: number;
  /** Oldest first. */
  asc?: boolean;
  /** Matches the procedure title, stage name or username. */
  q?: string;
  /** A choice (see `parseChoice`). */
  action?: string;
  /** One procedure's entries, e.g. from its page's View Audit Log. */
  procedureId?: string;
  from?: string;
  to?: string;
}

export function parseAuditSearch(raw: Record<string, unknown>): AuditListSearch {
  const procedureId = text(raw.procedureId);
  return compact({
    page: page(raw.page),
    asc: bool(raw.asc),
    q: text(raw.q),
    action: parseChoice(raw.action, AUDIT_ACTIONS),
    procedureId: procedureId && GUID.test(procedureId) ? procedureId : undefined,
    from: date(raw.from),
    to: date(raw.to),
  });
}

export const hasAuditFilters = (s: AuditListSearch) => Boolean(s.q || s.action || s.procedureId || s.from || s.to);

// Insights (admin) ------------------------------------------------------------------------------------

/** The periods Insights offers, in weeks (this one included). */
export const INSIGHTS_WEEKS = [4, 12, 26] as const;
export type InsightsWeeks = (typeof INSIGHTS_WEEKS)[number];
export const DEFAULT_INSIGHTS_WEEKS: InsightsWeeks = 12;

/** Insights' filters: the period (12 weeks unless the URL says otherwise) and a template (its ID). */
export interface InsightsSearch {
  weeks?: InsightsWeeks;
  template?: string;
}

export function parseInsightsSearch(raw: Record<string, unknown>): InsightsSearch {
  const weeks = INSIGHTS_WEEKS.find((w) => w === Number(text(raw.weeks)));
  const template = text(raw.template);
  return compact({
    weeks: weeks === DEFAULT_INSIGHTS_WEEKS ? undefined : weeks,
    template: template && GUID.test(template) ? template.toLowerCase() : undefined,
  });
}
