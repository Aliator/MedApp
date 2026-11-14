import {keepPreviousData, queryOptions} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import type {paths} from './schema';

/** Rows per page in every list (plan Q10). */
export const PAGE_SIZE = 10;

export type PatientSearch = NonNullable<paths['/api/patients/search']['get']['parameters']['query']>;
export type UserSearch = NonNullable<paths['/api/auth/users/search']['get']['parameters']['query']>;
export type ProcedureSearch = NonNullable<paths['/api/procedures/patient-procedures/search']['get']['parameters']['query']>;
export type PatientPage = Schemas['PatientResponsePagedResponse'];
export type UserPage = Schemas['UserResponsePagedResponse'];

/** Leaves out empty filters, so they aren't sent and equal searches share a cache entry. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(
    Object.entries(search).filter(([, value]) => value !== undefined && value !== null && value !== ''),
  ) as T;
}

/**
 * One page of patients from `GET api/patients/search`: filtered, sorted and paged by the API. While the next
 * page loads, the current one stays on screen (keepPreviousData), so the table doesn't flash empty.
 * Keyed under ['patients'], so a patient mutation that invalidates ['patients'] refreshes it.
 */
export function patientSearchQuery(search: PatientSearch) {
  const query = compact({PageSize: PAGE_SIZE, ...search});
  return queryOptions({
    queryKey: ['patients', 'search', query],
    queryFn: () => unwrap(api.GET('/api/patients/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

/** One page of users from `GET api/auth/users/search`, as patientSearchQuery. Keyed under ['users']. */
export function userSearchQuery(search: UserSearch) {
  const query = compact({PageSize: PAGE_SIZE, ...search});
  return queryOptions({
    queryKey: ['users', 'search', query],
    queryFn: () => unwrap(api.GET('/api/auth/users/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

/** One page of procedures from `GET api/procedures/patient-procedures/search`, as patientSearchQuery. Keyed under ['procedures']. */
export function procedureSearchQuery(search: ProcedureSearch) {
  const query = compact({PageSize: PAGE_SIZE, ...search});
  return queryOptions({
    queryKey: ['procedures', 'search', query],
    queryFn: () => unwrap(api.GET('/api/procedures/patient-procedures/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}
