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

/**
 * Confirms an email address with an emailed link (`POST api/auth/email/confirm`). It needs the link's own account
 * signed in: a 401 when signed out, and a 400 Identity error `WrongAccount` when someone else is signed in.
 */
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
 * Changes (or sets) the signed-in user's email address (`PUT api/auth/me/email`), given their password, so a session
 * someone else got hold of can't change where reset links go: the API emails a link to the new one, and the old one
 * stays in use until it's opened. The answer is the address as it now stands. While the account signs in with codes by
 * email, it also needs `code` (one emailed by `useSendTwoFactorEmailCode`, or a recovery code), since the address is
 * its second factor. A 400 may be on `email`, `password` or `code`; a 423 (locked), a 429 (too many changes) or a 409
 * (the email couldn't be sent) explains itself in its title.
 */
export function useChangeMyEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({email, password, code}: {email: string; password: string; code?: string}) =>
      toMyEmail(await unwrap(api.PUT('/api/auth/me/email', {body: {email, password, ...(code ? {code} : {})}}))),
    onSuccess: (email) => queryClient.setQueryData(myEmailQuery.queryKey, email),
  });
}

/**
 * Removes the signed-in user's email address, and any waiting to be confirmed (`DELETE api/auth/me/email`), given
 * their password (a wrong one is a 400 on `password`; a 423 when the account is locked).
 */
export function useRemoveMyEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (password: string) => unwrap(api.DELETE('/api/auth/me/email', {body: {password}})),
    onSuccess: () => queryClient.setQueryData(myEmailQuery.queryKey, {email: null, pendingEmail: null}),
  });
}

/** How an account's second step is made: none, a code from an authenticator app, or a code MedApp emails. */
export type TwoFactorMethod = 'None' | 'Authenticator' | 'Email';

/** Why codes by email can't be chosen: no confirmed address yet, or a server that can't send email. */
export type EmailCodesUnavailableReason = 'NoConfirmedAddress' | 'EmailUnavailable';

/** The signed-in user's two-factor authentication: on or off, how, recovery codes left, and whether they must use it. */
export interface TwoFactorStatus {
  enabled: boolean;
  method: TwoFactorMethod;
  recoveryCodesLeft: number;
  /**
   * They must use it (an admin, while Admin Settings requires it), so it can't be turned off. The requirement needs an
   * authenticator app: an admin using codes by email is held to switch.
   */
  required: boolean;
  /** Whether codes by email can be turned on (a confirmed address, and a server that can send email). */
  canUseEmailCodes: boolean;
  emailCodesUnavailableReason: EmailCodesUnavailableReason | null;
}

const toMethod = (method: string | null | undefined, enabled: boolean): TwoFactorMethod =>
  method === 'Email' ? 'Email' : method === 'Authenticator' || (enabled && method !== 'None') ? 'Authenticator' : 'None';

const toUnavailableReason = (reason: string | null | undefined): EmailCodesUnavailableReason | null =>
  reason === 'NoConfirmedAddress' || reason === 'EmailUnavailable' ? reason : null;

/** The signed-in user's two-factor authentication (`GET api/auth/me/two-factor`). */
export const twoFactorStatusQuery = queryOptions({
  queryKey: ['account', 'two-factor'],
  queryFn: async (): Promise<TwoFactorStatus> => {
    const body = await unwrap(api.GET('/api/auth/me/two-factor'));
    const enabled = body?.enabled === true;
    return {
      enabled,
      method: toMethod(body?.method, enabled),
      recoveryCodesLeft: body?.recoveryCodesLeft ?? 0,
      required: body?.required === true,
      canUseEmailCodes: body?.canUseEmailCodes === true,
      emailCodesUnavailableReason: toUnavailableReason(body?.emailCodesUnavailableReason),
    };
  },
});

/**
 * A two-factor code MedApp has just emailed: where it went (masked by the server, e.g. `j•••@example.com`), when it
 * stops working, how many seconds before another can be sent (by the server's clock, so a device with a wrong clock
 * still counts down right), and how many more can be sent.
 */
export interface EmailCodeSent {
  sentTo: string;
  expiresAtUtc: string;
  resendInSeconds: number;
  resendsLeft: number;
}

/** An emailed code's details from the API (`EmailCodeSentResponse`). */
export const toEmailCodeSent = (body: Schemas['EmailCodeSentResponse'] | null | undefined): EmailCodeSent => ({
  sentTo: body?.sentTo ?? '',
  expiresAtUtc: body?.expiresAtUtc ?? '',
  resendInSeconds: Math.max(0, body?.resendInSeconds ?? 0),
  resendsLeft: Math.max(0, body?.resendsLeft ?? 0),
});

/**
 * Starts turning on codes by email (`POST api/auth/me/two-factor/email`), given the password: the API emails a code to
 * the account's confirmed address. Asking again sends a new one (at most once a minute, three times). A 400 on
 * `password`; a 409 (already on, no confirmed address, or email can't be sent), 423 or 429 explains itself.
 */
export function useSendEmailCodesSetup() {
  return useMutation({
    mutationFn: async (password: string) =>
      toEmailCodeSent(await unwrap(api.POST('/api/auth/me/two-factor/email', {body: {password}}))),
  });
}

/**
 * Emails the signed-in user a code for confirming a change (`POST api/auth/me/two-factor/email/code`), when they sign
 * in with codes by email: turning it off, new recovery codes, switching to an app or changing the address. The password
 * comes first, as for setting codes up: a wrong one is a 400 on `password` (and counts towards the lockout, a 423). A
 * 409 when they don't use codes by email or the code couldn't be sent; a 429 when it's too soon.
 */
export function useSendTwoFactorEmailCode() {
  return useMutation({
    mutationFn: async (password: string) =>
      toEmailCodeSent(await unwrap(api.POST('/api/auth/me/two-factor/email/code', {body: {password}}))),
  });
}

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

/** The account's password with a two-factor code: what changing two-factor asks for, so a session alone can't. */
export interface PasswordAndCode {
  password: string;
  code: string;
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
 * Turns two-factor on with the password and a code from the app just set up (`POST api/auth/me/two-factor/enable`),
 * returning the recovery codes, once. Switching from codes by email also needs `currentCode`: one emailed by
 * `useSendTwoFactorEmailCode`, or a recovery code, so a session and the password alone can't replace the second factor.
 * A wrong password or code is a 400 on `password`, `code` or `currentCode`; a 423 when the account is locked. The
 * account's other sessions end.
 */
export function useEnableTwoFactor() {
  return useTwoFactorMutation(async ({password, code, currentCode}: PasswordAndCode & {currentCode?: string}) =>
    recoveryCodes(
      await unwrap(
        api.POST('/api/auth/me/two-factor/enable', {body: {password, code, ...(currentCode ? {currentCode} : {})}}),
      ),
    ),
  );
}

/**
 * Turns two-factor on with codes by email (`POST api/auth/me/two-factor/email/enable`), with the code emailed by
 * `useSendEmailCodesSetup`, returning the recovery codes, once. A wrong code is a 400 on `code`; the account's other
 * sessions end.
 */
export function useEnableEmailCodes() {
  return useTwoFactorMutation(async (code: string) =>
    recoveryCodes(await unwrap(api.POST('/api/auth/me/two-factor/email/enable', {body: {code}}))),
  );
}

/**
 * Turns two-factor off (`POST api/auth/me/two-factor/disable`), given the password and a current code (from the app, or
 * emailed for an account using codes by email, or a recovery code). A 409 when the account must use it, a 423
 * when it's locked, a 429 after too many codes; the account's other sessions end.
 */
export function useDisableTwoFactor() {
  return useTwoFactorMutation(({password, code}: PasswordAndCode) =>
    unwrap(api.POST('/api/auth/me/two-factor/disable', {body: {password, code}})),
  );
}

/**
 * Replaces the recovery codes with a new set (`POST api/auth/me/two-factor/recovery-codes`), given the password and a
 * current code (from the app, or emailed for an account using codes by email). A 400 on either; a 423 when the
 * account is locked, a 429 after too many codes.
 */
export function useRegenerateRecoveryCodes() {
  return useTwoFactorMutation(async ({password, code}: PasswordAndCode) =>
    recoveryCodes(await unwrap(api.POST('/api/auth/me/two-factor/recovery-codes', {body: {password, code}}))),
  );
}

/** The sign-in rules an admin sets (Admin Settings), with who changed them last. */
export interface SecuritySettings {
  requireTwoFactorForAdmins: boolean;
  /** Admin accounts that haven't set up two-factor authentication. */
  adminsWithoutTwoFactor: number;
  /** Their usernames. */
  adminsWithoutTwoFactorUsernames: string[];
  updatedAtUtc: string | null;
  updatedBy: string | null;
}

const toSecuritySettings = (body: Schemas['SecuritySettingsResponse'] | undefined): SecuritySettings => ({
  requireTwoFactorForAdmins: body?.requireTwoFactorForAdmins === true,
  adminsWithoutTwoFactor: body?.adminsWithoutTwoFactor ?? 0,
  adminsWithoutTwoFactorUsernames: [...(body?.adminsWithoutTwoFactorUsernames ?? [])],
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
