/**
 * The list pages keep their state in the URL, as the Blazor pages do, so a search or page is a shareable link:
 * `page`, `sort`, `asc`, and the search filters. These parse what's in the address bar into typed values,
 * dropping anything invalid, so a bad link shows the default list rather than an error.
 */

import {ACTIVITY_ACTIONS} from '../api/activity';
import {AUDIT_ACTIONS} from '../api/audit';
import {searchNow} from '../app/beforePathways';

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

// The clinic picker (MC6) ---------------------------------------------------------------------------------

/** The clinic picker's value for every team the person may see (`components/teams/ClinicPicker`). */
export const ALL_CLINICS = 'all';

/** The URL's `clinic` (the clinic picker): `all`, or a team's ID in lower case; anything else is dropped. */
export function parseClinic(value: unknown): string | undefined {
  const t = text(value)?.toLowerCase();
  return t === ALL_CLINICS || (t && GUID.test(t)) ? t : undefined;
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
  /** Digits of an NHS number (spaces ignored by the API), which the patient's number contains. */
  nhs?: string;
  /** The clinic picker's team, or `all` (see `parseClinic`). */
  clinic?: string;
  mine?: boolean;
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
    nhs: text(raw.nhs),
    clinic: parseClinic(raw.clinic),
    mine: bool(raw.mine),
  });
}

export const hasPatientFilters = (s: PatientsListSearch) =>
  Boolean(s.first || s.last || s.email || s.dobFrom || s.dobTo || s.nhs);

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

export const PATHWAY_STATUSES = ['Unassigned', 'Assigned', 'InProgress', 'InReview', 'Blocked', 'Completed', 'Cancelled', 'Archived'] as const;
export type PathwayStatus = (typeof PATHWAY_STATUSES)[number];
/** The statuses of a pathway still being worked on: the ones lists show unless asked for closed ones too. */
export const OPEN_PATHWAY_STATUSES = ['Unassigned', 'Assigned', 'InProgress', 'InReview', 'Blocked'] as const satisfies readonly PathwayStatus[];

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

/**
 * A choice filter of IDs (teams, stages): one ID, or a comma list ("is any of"), in lower case. Not "not any
 * of": the API takes the IDs wanted, and the full list isn't known when the URL is read. Anything else is dropped.
 */
export function parseIdChoice(value: unknown): string | undefined {
  const t = text(value);
  if (!t || t.startsWith('!')) return undefined;
  const ids = t
    .split(',')
    .map((v) => v.trim().toLowerCase())
    .filter((v) => GUID.test(v));
  return ids.length ? [...new Set(ids)].join(',') : undefined;
}

/** The IDs an ID choice filter (`parseIdChoice`) names, for the API; undefined for none. */
export const idChoiceValues = (value: string | undefined): string[] | undefined => (value ? value.split(',') : undefined);

// My Worklist ------------------------------------------------------------------------------------------------

/** URL sort names for My Worklist, and the API's names for them. Target date, soonest first, by default. */
export const TASK_SORTS = {Pathway: 'title', Due: 'dueDate', Priority: 'priority', Wait: 'daysWaited'} as const;
export type TaskSortColumn = keyof typeof TASK_SORTS;

export const TASK_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;

/**
 * My Worklist's team views (the relay, MC7), by URL name: the team's pathways rather than those assigned to me. Our
 * turn (a current stage my team works), New for us (handed to us, not picked up), Awaiting the lab (ours, at a lab
 * stage) and Back from the lab (ours, handed back, not picked up). The API's names for them.
 */
export const TASK_VIEWS = {ourTurn: 'OurTurn', newForUs: 'NewForUs', awaitingLab: 'AwaitingLab', backFromLab: 'BackFromLab'} as const;
export type TaskView = keyof typeof TASK_VIEWS;
export type TaskPriority = (typeof TASK_PRIORITIES)[number];

/**
 * My Worklist's page, sort and filters. `progress` is Not started unless the URL says otherwise; `any` (its filter
 * removed) shows both.
 */
export interface TasksSearch {
  page?: number;
  sort?: TaskSortColumn;
  asc?: boolean;
  /** The pathway's title contains this. */
  title?: string;
  template?: string;
  patient?: string;
  progress?: 'started' | 'any';
  /** A team view (`TASK_VIEWS`): the team's pathways in it, whoever they're assigned to, instead of mine. */
  view?: TaskView;
  /** A choice: one, a comma list, or `!` and a comma list (see `parseChoice`). */
  status?: string;
  priority?: string;
  /** Team IDs: an ID choice (see `parseIdChoice`). */
  team?: string;
  /** The stage reached, by stage definition IDs: an ID choice (see `parseIdChoice`). */
  stage?: string;
  overdue?: boolean;
  /** Only pathways whose referral's waiting-time clock has gone past its target. */
  breached?: boolean;
  dueFrom?: string;
  dueTo?: string;
}

export function parseTasksSearch(saved: Record<string, unknown>): TasksSearch {
  const raw = searchNow(saved);
  return compact({
    page: page(raw.page),
    sort: oneOf(raw.sort, Object.keys(TASK_SORTS) as TaskSortColumn[]),
    asc: bool(raw.asc),
    title: text(raw.title),
    template: text(raw.template),
    patient: text(raw.patient),
    progress: oneOf(raw.progress, ['started', 'any'] as const),
    view: oneOf(raw.view, Object.keys(TASK_VIEWS) as TaskView[]),
    status: parseChoice(raw.status, OPEN_PATHWAY_STATUSES),
    priority: parseChoice(raw.priority, TASK_PRIORITIES),
    team: parseIdChoice(raw.team),
    stage: parseIdChoice(raw.stage),
    overdue: bool(raw.overdue) ? true : undefined,
    breached: bool(raw.breached) ? true : undefined,
    dueFrom: date(raw.dueFrom),
    dueTo: date(raw.dueTo),
  });
}

/** Filters beyond the progress (which always applies, Not started by default). */
export const hasTaskFilters = (s: TasksSearch) =>
  Boolean(
    s.title ||
      s.template ||
      s.patient ||
      s.view ||
      s.status ||
      s.priority ||
      s.team ||
      s.stage ||
      s.overdue ||
      s.breached ||
      s.dueFrom ||
      s.dueTo,
  );

// Pathways --------------------------------------------------------------------------------------------

/**
 * URL sort names for the pathway lists, and the API's names for them. Target date, soonest first, by default. Wait is
 * the days waited on the referral's clock (pathways without one last, either way).
 */
export const PATHWAY_SORTS = {
  Pathway: 'title',
  Status: 'status',
  Due: 'dueDate',
  Priority: 'priority',
  Updated: 'lastUpdated',
  Wait: 'daysWaited',
} as const;
export type PathwaySortColumn = keyof typeof PATHWAY_SORTS;

/** The all-pathways list and a patient's pathways: page, sort and filters. */
export interface PathwaysListSearch {
  page?: number;
  sort?: PathwaySortColumn;
  asc?: boolean;
  /** The pathway's title contains this. */
  title?: string;
  template?: string;
  patient?: string;
  /** A choice (see `parseChoice`). Choosing closed statuses shows them even without `closed`. */
  status?: string;
  /** A choice (see `parseChoice`). */
  priority?: string;
  /** Team IDs (the pathways' own): an ID choice (see `parseIdChoice`), e.g. from Insights on a team. */
  team?: string;
  /**
   * The stage reached, by stage definition IDs: an ID choice (see `parseIdChoice`). Pathways whose current stage is
   * from one of them, not completed, cancelled or archived ("Waiting for histology").
   */
  stage?: string;
  overdue?: boolean;
  /** Only pathways whose referral's waiting-time clock has gone past its target. */
  breached?: boolean;
  /** Only pathways assigned to the signed-in user. `false` (in the URL) overrides starting on them (Account Settings). */
  mine?: boolean;
  /** Include Completed, Cancelled and Archived (hidden by default). */
  closed?: boolean;
  /** Who it's assigned to: a user's ID, or `none` for nobody (e.g. from Insights' overdue work by assignee). */
  assignee?: string;
  /** Made from this template (its ID), whatever it was called then (e.g. from Insights, on a template). */
  templateId?: string;
  dueFrom?: string;
  dueTo?: string;
  /** The clinic picker's team, or `all` (see `parseClinic`). */
  clinic?: string;
}

const flag = (value: unknown) => (bool(value) ? true : undefined);

/** The `assignee` filter's value for nobody assigned. */
export const NOBODY = 'none';

/** An ID, in lower case; anything else is dropped. */
function guid(value: unknown): string | undefined {
  const t = text(value)?.toLowerCase();
  return t && GUID.test(t) ? t : undefined;
}

/** An assignee filter: a user's ID, or `none`; anything else is dropped. */
function assignee(value: unknown): string | undefined {
  const t = text(value)?.toLowerCase();
  return t === NOBODY || (t && GUID.test(t)) ? t : undefined;
}

/**
 * The statuses a pathway list's search asks the API for: those chosen; for "not any of", the others (the open
 * ones only, unless closed ones are included too). Undefined for none chosen (the API's default: open ones).
 */
export function pathwayStatuses(search: {status?: string; closed?: boolean}): string[] | undefined {
  const pool = search.status?.startsWith('!') && !search.closed ? OPEN_PATHWAY_STATUSES : PATHWAY_STATUSES;
  return choiceValues(search.status, pool);
}

export function parsePathwaysSearch(saved: Record<string, unknown>): PathwaysListSearch {
  const raw = searchNow(saved);
  return compact({
    page: page(raw.page),
    sort: oneOf(raw.sort, Object.keys(PATHWAY_SORTS) as PathwaySortColumn[]),
    asc: bool(raw.asc),
    title: text(raw.title),
    template: text(raw.template),
    patient: text(raw.patient),
    status: parseChoice(raw.status, PATHWAY_STATUSES),
    priority: parseChoice(raw.priority, TASK_PRIORITIES),
    team: parseIdChoice(raw.team),
    stage: parseIdChoice(raw.stage),
    overdue: flag(raw.overdue),
    breached: flag(raw.breached),
    mine: bool(raw.mine),
    closed: flag(raw.closed),
    assignee: assignee(raw.assignee),
    templateId: guid(raw.templateId),
    dueFrom: date(raw.dueFrom),
    dueTo: date(raw.dueTo),
    clinic: parseClinic(raw.clinic),
  });
}

export const hasPathwayFilters = (s: PathwaysListSearch) =>
  Boolean(
    s.title ||
      s.template ||
      s.patient ||
      s.status ||
      s.priority ||
      s.team ||
      s.stage ||
      s.overdue ||
      s.breached ||
      s.mine ||
      s.closed ||
      s.assignee ||
      s.templateId ||
      s.dueFrom ||
      s.dueTo,
  );

// Pathway setup (admin): stage definitions and templates --------------------------------------------

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
  /** Matches the pathway title, stage name or username. */
  q?: string;
  /** A choice (see `parseChoice`). */
  action?: string;
  /** One pathway's entries, e.g. from its page's View Audit Log. */
  pathwayId?: string;
  from?: string;
  to?: string;
}

export function parseAuditSearch(saved: Record<string, unknown>): AuditListSearch {
  const raw = searchNow(saved);
  const pathwayId = text(raw.pathwayId);
  return compact({
    page: page(raw.page),
    asc: bool(raw.asc),
    q: text(raw.q),
    action: parseChoice(raw.action, AUDIT_ACTIONS),
    pathwayId: pathwayId && GUID.test(pathwayId) ? pathwayId : undefined,
    from: date(raw.from),
    to: date(raw.to),
  });
}

export const hasAuditFilters = (s: AuditListSearch) => Boolean(s.q || s.action || s.pathwayId || s.from || s.to);

// The activity log (admin) ----------------------------------------------------------------------------

/** The activity log: page, order (newest first unless `asc`) and filters. */
export interface ActivityListSearch {
  page?: number;
  /** Oldest first. */
  asc?: boolean;
  /** Matches the username, the account changed, or the address. */
  q?: string;
  /** A choice (see `parseChoice`). */
  action?: string;
  /** One patient's entries, e.g. from their page's access report. */
  patientId?: string;
  from?: string;
  to?: string;
}

export function parseActivitySearch(saved: Record<string, unknown>): ActivityListSearch {
  const raw = searchNow(saved);
  const patientId = text(raw.patientId);
  return compact({
    page: page(raw.page),
    asc: bool(raw.asc),
    q: text(raw.q),
    action: parseChoice(raw.action, ACTIVITY_ACTIONS),
    patientId: patientId && GUID.test(patientId) ? patientId : undefined,
    from: date(raw.from),
    to: date(raw.to),
  });
}

export const hasActivityFilters = (s: ActivityListSearch) => Boolean(s.q || s.action || s.patientId || s.from || s.to);

// The retention review list (admin) -------------------------------------------------------------------

/** The retention review list: page, and whether records an admin decided to keep are listed too. */
export interface RetentionListSearch {
  page?: number;
  kept?: boolean;
}

export function parseRetentionSearch(raw: Record<string, unknown>): RetentionListSearch {
  return compact({page: page(raw.page), kept: bool(raw.kept) || undefined});
}

// Insights (admin) ------------------------------------------------------------------------------------

/** The periods Insights offers, in weeks (this one included). */
export const INSIGHTS_WEEKS = [4, 12, 26] as const;
export type InsightsWeeks = (typeof INSIGHTS_WEEKS)[number];
export const DEFAULT_INSIGHTS_WEEKS: InsightsWeeks = 12;

/**
 * Insights' filters: the period (12 weeks unless the URL says otherwise), the clinic picker's team (or `all`) and a
 * template (its ID). Its team was `team` before the clinic picker (MC6), and still reads that way.
 */
export interface InsightsSearch {
  weeks?: InsightsWeeks;
  clinic?: string;
  template?: string;
}

export function parseInsightsSearch(saved: Record<string, unknown>): InsightsSearch {
  const raw = searchNow(saved);
  const weeks = INSIGHTS_WEEKS.find((w) => w === Number(text(raw.weeks)));
  return compact({
    weeks: weeks === DEFAULT_INSIGHTS_WEEKS ? undefined : weeks,
    clinic: parseClinic(raw.clinic) ?? guid(raw.team),
    template: guid(raw.template),
  });
}
