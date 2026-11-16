import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap} from './client';
import {ApiError} from './errors';

/** Every role (`GET api/auth/roles`), e.g. Admin and User. */
export const rolesQuery = queryOptions({
  queryKey: ['roles'],
  queryFn: async () => [...((await unwrap(api.GET('/api/auth/roles'))) ?? [])].sort(),
  staleTime: 5 * 60 * 1000,
});

/**
 * Every users query is keyed under ['users'], so writes refresh them all. `carriesPassword` is for a mutation whose
 * result is a generated password: it's dropped from the mutation cache as soon as nothing shows it (gcTime 0), rather
 * than kept for the default five minutes.
 */
function useUserMutation<TInput, TResult>(mutationFn: (input: TInput) => Promise<TResult>, {carriesPassword = false} = {}) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({queryKey: ['users']}),
    ...(carriesPassword && {gcTime: 0}),
  });
}

/**
 * A new account and the first password the server generated for it, which is shown to the admin only once and works
 * until the user changes it.
 */
export interface CreatedUser {
  username: string;
  password: string;
}

/** A new account: its username, and (optionally) an email address, to which the API sends a link to confirm it. */
export interface NewUser {
  username: string;
  email?: string;
}

/**
 * Creates an account. The server generates its first password and returns it, this once, to be passed on (it's never
 * emailed). With an email address, the API emails it a link to confirm it.
 */
export function useCreateUser() {
  return useUserMutation(async ({username, email}: NewUser): Promise<CreatedUser> => {
    const body = await unwrap(api.POST('/api/auth/users', {body: {username, ...(email ? {email} : {})}}));
    if (!body?.password) throw new ApiError(500, 'The API did not return a password for the new user.');
    return {username, password: body.password};
  }, {carriesPassword: true});
}

/** Deletes a user, confirmed with the signed-in admin's own password (a wrong one is a 400 the API explains). */
export function useDeleteUser() {
  return useUserMutation(({username, password}: {username: string; password: string}) =>
    unwrap(api.DELETE('/api/auth/users/{username}', {params: {path: {username}}, body: {password}})),
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
  }, {carriesPassword: true});
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

/**
 * Signs a user out everywhere: every session they have ends, on every browser and device, and they must sign in again.
 * Their account and password are unchanged.
 */
export function useSignOutEverywhere() {
  return useUserMutation((username: string) =>
    unwrap(api.POST('/api/auth/users/{username}/sign-out-everywhere', {params: {path: {username}}})),
  );
}

/**
 * Turns off a user's two-factor authentication (their authenticator key and recovery codes go too) and signs them out
 * everywhere, for someone who has lost their authenticator app. Admins can't reset their own (the API refuses).
 */
export function useResetTwoFactor() {
  return useUserMutation((username: string) =>
    unwrap(api.POST('/api/auth/users/{username}/reset-two-factor', {params: {path: {username}}})),
  );
}

export interface RoleChange {
  username: string;
  roles: string[];
}

/**
 * Sets a user's roles to exactly these, in one step: the API adds the new ones and removes the rest together, so a
 * failure can't leave them half-changed. Admins can't change their own roles (the API refuses).
 */
export function useSetRoles() {
  return useUserMutation(({username, roles}: RoleChange) =>
    unwrap(api.PUT('/api/auth/users/{username}/roles', {params: {path: {username}}, body: {roles}})),
  );
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
