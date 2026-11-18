import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';

/**
 * Roles (`api/roles`): admins make them, choose each one's permissions, archive and restore them, and delete ones
 * nobody holds. The built-in Admin role can't be changed; the Administrator-only permissions can't be put on any other
 * role. Every change sends the role's `version` as `expectedVersion`, so another admin's change made meanwhile is a
 * 409. Giving people roles is the Users page's.
 */
export type Role = Schemas['RoleResponse'];
export type RoleHolder = Schemas['RoleHolderResponse'];

/** Every role query is keyed under ['roles'], so every change refreshes them all (the Users page's picker too). */
export const roleKeys = {
  all: ['roles'] as const,
  list: ['roles', 'list'] as const,
  one: (id: string) => ['roles', 'one', id] as const,
};

/** Every role, archived ones too: Admin first, then by name (as the API sends them). */
export const rolesQuery = queryOptions({
  queryKey: roleKeys.list,
  queryFn: () => unwrap(api.GET('/api/roles')),
  staleTime: 60 * 1000,
});

/** One role, with who holds it. An unknown one is an ApiError with status 404. */
export function roleQuery(id: string) {
  return queryOptions({
    queryKey: roleKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/roles/{id}', {params: {path: {id}}})),
  });
}

/** Every role's name, sorted: the Users page's filter (archived ones too, as people may still hold them). */
export const roleNames = (roles: readonly Role[]) =>
  roles
    .map((r) => r.name ?? '')
    .filter(Boolean)
    .sort((a, b) => a.localeCompare(b));

/**
 * The roles people can be given (the Users page's picker): those that aren't archived, as `{value, label,
 * description}`, plus any archived one in `keep` (someone holds it already: they keep it, but nobody new is given it),
 * marked as archived.
 */
export function roleChoices(roles: readonly Role[], keep: readonly string[] = []) {
  return roles
    .filter((r) => !r.isArchived || keep.includes(r.name ?? ''))
    .map((r) => ({
      value: r.name ?? '',
      label: r.isArchived ? `${r.name} (archived)` : (r.name ?? ''),
      description: r.description || undefined,
    }));
}

/**
 * What a change refreshes: every role (the list counts, the picker) and the users (whose roles' names may change).
 * After a 409 (someone changed the role meanwhile, or the name is taken) the roles are read again too, so the page
 * shows what's there now, with its new version.
 */
function useRefreshRoles() {
  const queryClient = useQueryClient();
  const onSuccess = async () => {
    await queryClient.invalidateQueries({queryKey: roleKeys.all});
    await queryClient.invalidateQueries({queryKey: ['users']});
  };
  const onError = (error: unknown) => (error instanceof ApiError && error.status === 409 ? onSuccess() : undefined);
  return {onSuccess, onError};
}

/** A role's name, description and permissions; for a change, its ID and the version the admin started from. */
export interface RoleInput {
  id?: string;
  name: string;
  description: string;
  permissions: string[];
  expectedVersion?: string;
}

/** Makes a role (no `id`) or changes one, returning it. A name already taken, or a stale version, is a 409. */
export function useSaveRole() {
  const refresh = useRefreshRoles();
  return useMutation({
    mutationFn: ({id, name, description, permissions, expectedVersion}: RoleInput) =>
      id
        ? unwrap(
            api.PUT('/api/roles/{id}', {
              params: {path: {id}},
              body: {name, description, permissions, expectedVersion},
            }),
          )
        : unwrap(
            api.POST('/api/roles', {
              body: {name, description, permissions},
            }),
          ),
    ...refresh,
  });
}

/** A role and the version the admin saw, for archiving or restoring it. */
export interface RoleVersion {
  id: string;
  expectedVersion: string;
}

/** Archive, restore and delete (only a role nobody holds: `canDelete`; a 409 otherwise). */
export function useRoleActions() {
  const refresh = useRefreshRoles();
  return {
    archive: useMutation({
      mutationFn: ({id, expectedVersion}: RoleVersion) =>
        unwrap(
          api.POST('/api/roles/{id}/archive', {
            params: {path: {id}},
            body: {expectedVersion},
          }),
        ),
      ...refresh,
    }),
    restore: useMutation({
      mutationFn: ({id, expectedVersion}: RoleVersion) =>
        unwrap(
          api.POST('/api/roles/{id}/restore', {
            params: {path: {id}},
            body: {expectedVersion},
          }),
        ),
      ...refresh,
    }),
    remove: useMutation({
      mutationFn: (id: string) => unwrap(api.DELETE('/api/roles/{id}', {params: {path: {id}}})),
      ...refresh,
    }),
  };
}

/** "1 person" or "6 people". */
export const people = (count: number) => (count === 1 ? '1 person' : `${count} people`);
