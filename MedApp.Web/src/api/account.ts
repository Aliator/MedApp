import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

/**
 * What sign-in can do on this server (`GET api/auth/features`, anonymous). `canSendEmail` is false when the server has
 * no way to send email: then nothing offers what needs it (a reset link, an email address to confirm).
 */
export const featuresQuery = queryOptions({
  queryKey: ['auth', 'features'],
  queryFn: async () => {
    const body = await unwrap(api.GET('/api/auth/features'));
    return {canSendEmail: body?.canSendEmail === true};
  },
  staleTime: 5 * 60 * 1000,
  retry: false,
});

/** Asks for a password reset link (`POST api/auth/password/forgot`). Always accepted, whether or not anything matched. */
export function useForgotPassword() {
  return useMutation({
    mutationFn: (usernameOrEmail: string) => unwrap(api.POST('/api/auth/password/forgot', {body: {usernameOrEmail}})),
  });
}

/** A link's account and token, from an emailed link's fragment (`useEmailLink`). */
export interface EmailLinkToken {
  userId: string;
  token: string;
}

/** Chooses a new password with an emailed reset link (`POST api/auth/password/reset`). It signs the account out everywhere. */
export function useResetPasswordWithLink() {
  return useMutation({
    mutationFn: ({userId, token, newPassword}: EmailLinkToken & {newPassword: string}) =>
      unwrap(api.POST('/api/auth/password/reset', {body: {userId, token, newPassword}})),
  });
}

/** Confirms an email address with an emailed link (`POST api/auth/email/confirm`); anyone holding the link can. */
export function useConfirmEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({userId, token}: EmailLinkToken) => unwrap(api.POST('/api/auth/email/confirm', {body: {userId, token}})),
    // If it's the signed-in user's own, their email page shows it confirmed.
    onSuccess: () => queryClient.invalidateQueries({queryKey: myEmailQuery.queryKey}),
  });
}

/** The signed-in user's email address: the confirmed one in use, and one waiting to be confirmed. */
export interface MyEmail {
  email: string | null;
  pendingEmail: string | null;
}

const toMyEmail = (body: Schemas['EmailResponse'] | undefined): MyEmail => ({
  email: body?.email ?? null,
  pendingEmail: body?.pendingEmail ?? null,
});

/** The signed-in user's email address (`GET api/auth/me/email`). */
export const myEmailQuery = queryOptions({
  queryKey: ['account', 'email'],
  queryFn: async () => toMyEmail(await unwrap(api.GET('/api/auth/me/email'))),
});

/**
 * Changes (or sets) the signed-in user's email address (`PUT api/auth/me/email`): the API emails a link to the new
 * one, and the old one stays in use until it's opened. The answer is the address as it now stands.
 */
export function useChangeMyEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (email: string) => toMyEmail(await unwrap(api.PUT('/api/auth/me/email', {body: {email}}))),
    onSuccess: (email) => queryClient.setQueryData(myEmailQuery.queryKey, email),
  });
}

/** Removes the signed-in user's email address, and any waiting to be confirmed (`DELETE api/auth/me/email`). */
export function useRemoveMyEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/auth/me/email')),
    onSuccess: () => queryClient.setQueryData(myEmailQuery.queryKey, {email: null, pendingEmail: null}),
  });
}

/** The signed-in user's two-factor authentication: on or off, recovery codes left, and whether they must use it. */
export interface TwoFactorStatus {
  enabled: boolean;
  recoveryCodesLeft: number;
  /** They must use it (an admin, while Admin Settings requires it), so it can't be turned off. */
  required: boolean;
}

/** The signed-in user's two-factor authentication (`GET api/auth/me/two-factor`). */
export const twoFactorStatusQuery = queryOptions({
  queryKey: ['account', 'two-factor'],
  queryFn: async (): Promise<TwoFactorStatus> => {
    const body = await unwrap(api.GET('/api/auth/me/two-factor'));
    return {enabled: body?.enabled === true, recoveryCodesLeft: body?.recoveryCodesLeft ?? 0, required: body?.required === true};
  },
});

/** A new authenticator key: as a QR code (one SVG path of its dark modules, in a square `size` wide) and as text. */
export interface AuthenticatorSetup {
  sharedKey: string;
  qrCodePath: string;
  qrCodeSize: number;
}

/**
 * Starts setting up an authenticator app (`POST api/auth/me/two-factor/authenticator`): a new key, replacing any
 * earlier one not yet turned on. Two-factor stays off until `useEnableTwoFactor` gets a code from it. The key is a
 * secret, so the mutation forgets it as soon as nothing shows it (gcTime 0).
 */
export function useBeginAuthenticatorSetup() {
  return useMutation({
    mutationFn: async (): Promise<AuthenticatorSetup> => {
      const body = await unwrap(api.POST('/api/auth/me/two-factor/authenticator'));
      return {sharedKey: body?.sharedKey ?? '', qrCodePath: body?.qrCodePath ?? '', qrCodeSize: body?.qrCodeSize ?? 0};
    },
    gcTime: 0,
  });
}

/** Recovery codes from the API, shown once: each signs in once without the authenticator app. */
const recoveryCodes = (body: Schemas['RecoveryCodesResponse'] | undefined) => [...(body?.recoveryCodes ?? [])];

/**
 * Two-factor's mutations: each changes the signed-in user's two-factor state, so it's read again after. Those that
 * return recovery codes forget them as soon as nothing shows them (gcTime 0).
 */
function useTwoFactorMutation<TInput, TResult>(mutationFn: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({queryKey: twoFactorStatusQuery.queryKey}),
    gcTime: 0,
  });
}

/**
 * Turns two-factor on with a code from the app just set up (`POST api/auth/me/two-factor/enable`), returning the
 * recovery codes, once. A wrong code is a 400 on `code`. The account's other sessions end.
 */
export function useEnableTwoFactor() {
  return useTwoFactorMutation(async (code: string) =>
    recoveryCodes(await unwrap(api.POST('/api/auth/me/two-factor/enable', {body: {code}}))),
  );
}

/**
 * Turns two-factor off (`POST api/auth/me/two-factor/disable`), given the password and a current code. A 409 when the
 * account must use it; the account's other sessions end.
 */
export function useDisableTwoFactor() {
  return useTwoFactorMutation(({password, code}: {password: string; code: string}) =>
    unwrap(api.POST('/api/auth/me/two-factor/disable', {body: {password, code}})),
  );
}

/** Replaces the recovery codes with a new set, given a current code (`POST api/auth/me/two-factor/recovery-codes`). */
export function useRegenerateRecoveryCodes() {
  return useTwoFactorMutation(async (code: string) =>
    recoveryCodes(await unwrap(api.POST('/api/auth/me/two-factor/recovery-codes', {body: {code}}))),
  );
}

/** The sign-in rules an admin sets (Admin Settings), with who changed them last. */
export interface SecuritySettings {
  requireTwoFactorForAdmins: boolean;
  /** Admin accounts that haven't set up two-factor authentication. */
  adminsWithoutTwoFactor: number;
  updatedAtUtc: string | null;
  updatedBy: string | null;
}

const toSecuritySettings = (body: Schemas['SecuritySettingsResponse'] | undefined): SecuritySettings => ({
  requireTwoFactorForAdmins: body?.requireTwoFactorForAdmins === true,
  adminsWithoutTwoFactor: body?.adminsWithoutTwoFactor ?? 0,
  updatedAtUtc: body?.updatedAtUtc ?? null,
  updatedBy: body?.updatedBy ?? null,
});

/** The sign-in rules (`GET api/auth/security-settings`, admin only). */
export const securitySettingsQuery = queryOptions({
  queryKey: ['security-settings'],
  queryFn: async () => toSecuritySettings(await unwrap(api.GET('/api/auth/security-settings'))),
});

/**
 * Saves the sign-in rules (`PUT api/auth/security-settings`). A 409 says why it can't (an admin must set up two-factor
 * for their own account before requiring it). The answer is the rules as they now stand.
 */
export function useSaveSecuritySettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (requireTwoFactorForAdmins: boolean) =>
      toSecuritySettings(await unwrap(api.PUT('/api/auth/security-settings', {body: {requireTwoFactorForAdmins}}))),
    onSuccess: (settings) => queryClient.setQueryData(securitySettingsQuery.queryKey, settings),
  });
}
