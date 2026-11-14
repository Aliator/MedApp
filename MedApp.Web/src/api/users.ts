import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap} from './client';
import {ApiError} from './errors';

/**
 * The password new accounts get, as in the Blazor app, which shows it in the create-user dialog.
 * Users should change it from their Account page.
 */
export const DEFAULT_NEW_USER_PASSWORD = 'DefaultPassword1';

/** Every role (`GET api/auth/roles`), e.g. Admin and User. */
export const rolesQuery = queryOptions({
  queryKey: ['roles'],
  queryFn: async () => [...((await unwrap(api.GET('/api/auth/roles'))) ?? [])].sort(),
  staleTime: 5 * 60 * 1000,
});

/** Every users query is keyed under ['users'], so writes refresh them all. */
function useUserMutation<TInput, TResult>(mutationFn: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({queryKey: ['users']}),
  });
}

export function useCreateUser() {
  return useUserMutation((username: string) =>
    unwrap(api.POST('/api/auth/users', {body: {username, password: DEFAULT_NEW_USER_PASSWORD}})),
  );
}

export function useDeleteUser() {
  return useUserMutation((username: string) =>
    unwrap(api.DELETE('/api/auth/users/{username}', {params: {path: {username}}})),
  );
}

/** Gives the user a new random password and returns it, to be passed on to them. */
export function useResetPassword() {
  return useUserMutation(async (username: string): Promise<string> => {
    // ASP.NET sends a bare string as text/plain unless the client asks for JSON, so read the body as text and
    // accept either form.
    const body = await unwrap(
      api.PUT('/api/auth/users/{username}/reset-password', {params: {path: {username}}, parseAs: 'text'}),
    );
    const password = parsePasswordBody(body);
    if (!password) throw new ApiError(500, 'The API did not return a new password.');
    return password;
  });
}

/** The reset-password body: a JSON string ("abc") or the bare text (abc). */
export function parsePasswordBody(body: unknown): string | null {
  if (typeof body !== 'string') return null;
  const text = body.trim();
  if (text.startsWith('"')) {
    try {
      const parsed: unknown = JSON.parse(text);
      return typeof parsed === 'string' && parsed ? parsed : null;
    } catch {
      return null;
    }
  }
  return text || null;
}

export interface RoleChange {
  username: string;
  roles: string[];
}

/** Assigns each role in turn (the API takes one at a time). */
export function useAssignRoles() {
  return useUserMutation(async ({username, roles}: RoleChange) => {
    for (const role of roles) await unwrap(api.POST('/api/auth/roles/assign', {body: {username, role}}));
  });
}

/** Revokes each role in turn (the API takes one at a time). */
export function useRevokeRoles() {
  return useUserMutation(async ({username, roles}: RoleChange) => {
    for (const role of roles) await unwrap(api.POST('/api/auth/roles/revoke', {body: {username, role}}));
  });
}

export interface PasswordChange {
  username: string;
  oldPassword: string;
  newPassword: string;
}

/** Changes the signed-in user's own password. */
export function useChangePassword() {
  return useMutation({
    mutationFn: ({username, oldPassword, newPassword}: PasswordChange) =>
      unwrap(
        api.PUT('/api/auth/users/{username}/update-password', {
          params: {path: {username}},
          body: {oldPassword, newPassword},
        }),
      ),
  });
}
