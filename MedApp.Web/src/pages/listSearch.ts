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
