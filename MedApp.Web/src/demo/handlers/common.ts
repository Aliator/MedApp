import type {Schemas} from '../../api/client';
import type {Permission} from '../../auth/permissions';
import {personByUsername, type DemoMembership, type DemoPerson} from '../accounts';
import {DemoReply, problem, refuse} from '../api/http';
import {db} from '../store';

/** Who's signed in to the light demo, or null. */
export function signedIn(): DemoPerson | null {
  return personByUsername(db().user) ?? null;
}

/**
 * The signed-in person. Like the API, refuses with a 401 when nobody is (the layout's guard then goes to the login
 * page), so handlers call it first.
 */
export function me(): DemoPerson {
  const person = signedIn();
  if (!person) refuse(problem(401, 'Sign in to continue.'));
  return person;
}

/** Someone's teams and their role in each, as they are now (an admin may have changed them). */
export const membershipsOf = (person: DemoPerson): DemoMembership[] => db().memberships[person.id] ?? person.teams;

/** The IDs of someone's teams, as they are now. */
export const teamIdsOf = (person: DemoPerson): string[] => membershipsOf(person).map((m) => m.teamId);

/** Whether the signed-in person's roles grant a permission. */
export const can = (permission: Permission) => signedIn()?.permissions.includes(permission) ?? false;

/** Refuses as the API does when a role doesn't grant what's asked. */
export function requirePermission(permission: Permission) {
  me();
  if (!can(permission))
    refuse(
      new DemoReply(403, {
        type: 'about:blank',
        title: 'Forbidden',
        status: 403,
        code: 'permission_missing',
        permission,
        detail: `Your roles don't allow this. It needs the permission: ${permission}.`,
      }),
    );
}

/** A query value as a number, or the fallback. */
export function int(query: URLSearchParams, name: string, fallback: number): number {
  const value = Number(query.get(name));
  return Number.isFinite(value) && value > 0 ? value : fallback;
}

/** A query value as a boolean (`true`), or undefined. */
export function flag(query: URLSearchParams, name: string): boolean | undefined {
  const value = query.get(name);
  return value === null ? undefined : value === 'true';
}

/** A query's repeated values (`Statuses=A&Statuses=B`). */
export const list = (query: URLSearchParams, name: string) => query.getAll(name).filter(Boolean);

/** Whether `text` holds `search`, ignoring case (an empty search matches all). */
export const contains = (text: string | null | undefined, search: string | null) =>
  !search || (text ?? '').toLowerCase().includes(search.toLowerCase());

/** One page of a list, as the API's `PagedResponse<T>`. */
export function paged<T>(items: T[], query: URLSearchParams): {items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number} {
  const pageSize = Math.min(int(query, 'PageSize', 10), 100);
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
  const page = Math.min(int(query, 'Page', 1), totalPages);
  return {items: items.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: items.length, totalPages};
}

/** Sorts by one of a list's sort keys (`Sort`, `Ascending`), then by ID so paging is stable. */
export function sorted<T extends {id?: string}>(
  items: T[],
  query: URLSearchParams,
  keys: Record<string, (item: T) => string | number>,
  fallback: string,
): T[] {
  const key = keys[query.get('Sort') ?? ''] ?? keys[fallback];
  const ascending = query.get('Ascending') !== 'false';
  return [...items].sort((a, b) => {
    const x = key(a);
    const y = key(b);
    const order = x < y ? -1 : x > y ? 1 : (a.id ?? '').localeCompare(b.id ?? '');
    return ascending ? order : -order;
  });
}

/**
 * Sorts by one of a list's columns (`keys[sort]`) in the direction asked, then by ID so paging is stable; null for a
 * column the list doesn't sort by, so it keeps its own order.
 */
export function sortedBy<T extends {id?: string | null}>(
  items: T[],
  sort: string | null | undefined,
  ascending: boolean,
  keys: Record<string, (item: T) => string | number>,
): T[] | null {
  const key = sort ? keys[sort.toLowerCase()] : undefined;
  if (!key) return null;
  return [...items].sort((a, b) => {
    const x = key(a);
    const y = key(b);
    const order = x < y ? -1 : x > y ? 1 : (a.id ?? '').localeCompare(b.id ?? '');
    return ascending ? order : -order;
  });
}

/** A patient's name as lists sort it: last name, then first, ignoring case. */
export function patientSortName(patientId: string | null | undefined): string {
  const patient = db().patients.find((p) => p.id === patientId);
  return `${patient?.lastName ?? ''} ${patient?.firstName ?? ''}`.toLowerCase();
}

/** A patient's name, "First Last". */
export function patientName(patientId: string | null | undefined): string {
  const patient = db().patients.find((p) => p.id === patientId);
  return patient ? `${patient.firstName} ${patient.lastName}` : 'Unknown patient';
}

/** A patient as a pathway or referral carries them (`PathwayPatientResponse`). */
export function pathwayPatient(patientId: string): Schemas['PathwayPatientResponse'] {
  const patient = db().patients.find((p) => p.id === patientId);
  return {
    id: patientId,
    firstName: patient?.firstName ?? null,
    lastName: patient?.lastName ?? null,
    dateOfBirth: patient?.dateOfBirth,
    nhsNumber: patient?.nhsNumber ?? null,
    isRestricted: patient?.isRestricted ?? false,
    isDisputed: patient?.isDisputed ?? false,
    isAnonymised: patient?.isAnonymised ?? false,
  };
}

export const NO_FLAGS: Schemas['PatientFlagsResponse'] = {isRestricted: false, isDisputed: false, isAnonymised: false};
