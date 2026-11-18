import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';
import {setupKeys} from './pathwaySetup';

/**
 * Specialties (Dermatology, Urology, ...): admins keep the list; each pathway template belongs to one, and members of
 * staff are given theirs on the Users page. Anyone can read them; only admins change them. A specialty in use can only
 * be archived, which keeps it where it is but stops it being newly chosen.
 */
export type Specialty = Schemas['SpecialtyResponse'];
/** One of a member of staff's specialties: its ID, name and whether it's archived. */
export type StaffSpecialty = Schemas['StaffSpecialtyResponse'];

const SPECIALTIES = '/api/specialties';
const SPECIALTY = '/api/specialties/{id}';

/** Every specialty query is keyed under ['specialties'], so every change refreshes them all. */
export const specialtyKeys = {
  all: ['specialties'] as const,
  list: (includeArchived: boolean) => ['specialties', 'list', includeArchived ? 'all' : 'active'] as const,
  ofUser: (username: string) => ['specialties', 'user', username.toLowerCase()] as const,
  mine: ['specialties', 'mine'] as const,
};

/** The specialties, sorted by name: only the active ones, or archived ones too (they carry `isArchived`). */
export function specialtiesQuery(includeArchived: boolean) {
  return queryOptions({
    queryKey: specialtyKeys.list(includeArchived),
    queryFn: () => unwrap(api.GET(SPECIALTIES, {params: {query: {includeArchived}}})),
  });
}

/** A member of staff's specialties (admins). An unknown user is an ApiError with status 404. */
export function userSpecialtiesQuery(username: string) {
  return queryOptions({
    queryKey: specialtyKeys.ofUser(username),
    queryFn: () => unwrap(api.GET('/api/auth/users/{username}/specialties', {params: {path: {username}}})),
  });
}

/** The signed-in user's own specialties. */
export const mySpecialtiesQuery = queryOptions({
  queryKey: specialtyKeys.mine,
  queryFn: () => unwrap(api.GET('/api/auth/me/specialties')),
});

/**
 * What a change refreshes: specialties and templates together (a template shows its specialty's name, and a specialty
 * counts its templates). After a 409 (someone changed it meanwhile, or the name is taken) they're read again too, so
 * the page shows what's there now, with its new version.
 */
function useRefreshSpecialties() {
  const queryClient = useQueryClient();
  const onSuccess = async () => {
    await queryClient.invalidateQueries({queryKey: specialtyKeys.all});
    await queryClient.invalidateQueries({queryKey: setupKeys.templates});
  };
  const onError = (error: unknown) => (error instanceof ApiError && error.status === 409 ? onSuccess() : undefined);
  return {onSuccess, onError};
}

/** A specialty's name, and for a rename the version the admin started from (`expectedVersion`), so a stale one is a 409. */
export interface SpecialtyInput {
  id?: string;
  name: string;
  expectedVersion?: number;
}

/** Adds a specialty (no `id`) or renames one, returning it. A name already taken is a 409. */
export function useSaveSpecialty() {
  const refresh = useRefreshSpecialties();
  return useMutation({
    mutationFn: ({id, name, expectedVersion}: SpecialtyInput) =>
      id
        ? unwrap(api.PUT(SPECIALTY, {params: {path: {id}}, body: {name, expectedVersion}}))
        : unwrap(api.POST(SPECIALTIES, {body: {name}})),
    ...refresh,
  });
}

/** A specialty and the version the admin saw, for archiving or restoring it. */
export interface SpecialtyVersion {
  id: string;
  expectedVersion: number;
}

/**
 * Archive, restore and delete. Archiving and restoring send the version shown, so a change made meanwhile is a 409;
 * only a specialty nothing uses can be deleted (`canBeDeleted`; a 409 otherwise).
 */
export function useSpecialtyActions() {
  const refresh = useRefreshSpecialties();
  return {
    archive: useMutation({
      mutationFn: ({id, expectedVersion}: SpecialtyVersion) =>
        unwrap(api.POST('/api/specialties/{id}/archive', {params: {path: {id}}, body: {expectedVersion}})),
      ...refresh,
    }),
    restore: useMutation({
      mutationFn: ({id, expectedVersion}: SpecialtyVersion) =>
        unwrap(api.POST('/api/specialties/{id}/restore', {params: {path: {id}}, body: {expectedVersion}})),
      ...refresh,
    }),
    remove: useMutation({
      mutationFn: (id: string) => unwrap(api.DELETE(SPECIALTY, {params: {path: {id}}})),
      ...refresh,
    }),
  };
}

/**
 * Sets a member of staff's specialties to exactly these (admins), returning them. An archived one they already have
 * can be kept, but not newly given (409); an unknown user or specialty is a 404.
 */
export function useSetUserSpecialties() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({queryKey: specialtyKeys.all});
  return useMutation({
    mutationFn: ({username, specialtyIds}: {username: string; specialtyIds: string[]}) =>
      unwrap(api.PUT('/api/auth/users/{username}/specialties', {params: {path: {username}}, body: {specialtyIds}})),
    // Their specialties, the members counts, and the admin's own if they were the one changed; after a 409 (a
    // specialty archived meanwhile) too, so the dialog shows it archived.
    onSuccess: refresh,
    onError: (error) => (error instanceof ApiError && error.status === 409 ? refresh() : undefined),
  });
}

/** Specialties in words: "Dermatology, Urology", or `none` when there are none. */
export function specialtyNames(specialties: readonly StaffSpecialty[], none: string): string {
  const names = specialties.map((s) => s.name ?? '').filter(Boolean);
  return names.length ? names.join(', ') : none;
}
