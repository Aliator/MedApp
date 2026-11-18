import {queryOptions, useMutation, useQueryClient, useSuspenseQuery, type QueryClient} from '@tanstack/react-query';
import {redirect, useNavigate} from '@tanstack/react-router';
import {toEmailCodeSent, type EmailCodeSent} from '../api/account';
import {api, unwrap, type Schemas} from '../api/client';
import {settingsQuery} from '../api/settings';
import {currentStartPagePath} from '../app/navSettings';
import {appSettings, clearStorageAtSignOut, flushSettings} from '../app/settingsStore';

export interface CurrentUser {
  isAuthenticated: boolean;
  username: string;
  roles: string[];
  isAdmin: boolean;
  /**
   * They were given a generated password (a new account, or an admin's reset) and must choose their own before
   * anything else: the API refuses everything but that, so the signed-in layout sends them to `/change-password`.
   */
  mustChangePassword: boolean;
  /**
   * An admin who must use two-factor authentication (Admin Settings requires it) but hasn't set it up: the API refuses
   * everything but that, so the signed-in layout sends them to `/set-up-two-factor`. A password they must change
   * comes first.
   */
  mustSetUpTwoFactor: boolean;
}

const signedOut: CurrentUser = {
  isAuthenticated: false,
  username: '',
  roles: [],
  isAdmin: false,
  mustChangePassword: false,
  mustSetUpTwoFactor: false,
};

/** Who is signed in, from `GET api/auth/whoami`. Fetched once and cached until login or logout (replaces Blazor's AuthState). */
export const whoamiQuery = queryOptions({
  queryKey: ['whoami'],
  queryFn: async (): Promise<CurrentUser> => {
    const {data, response} = await api.GET('/api/auth/whoami');
    if (!response.ok || !data?.isAuthenticated) return signedOut;
    const roles = [...(data.roles ?? [])];
    return {
      isAuthenticated: true,
      username: data.name ?? '',
      roles,
      isAdmin: roles.some((r) => r.toLowerCase() === 'admin'),
      mustChangePassword: data.mustChangePassword === true,
      mustSetUpTwoFactor: data.mustSetUpTwoFactor === true,
    };
  },
  staleTime: Infinity,
});

/**
 * The signed-in layout's guard (`_app`): anyone signed out goes to sign in, anyone who must change the password they
 * were given goes to do that, and then an admin who must set up two-factor authentication goes to do that, before the
 * layout loads anything the API would refuse them.
 */
export async function requireSignedIn({context}: {context: {queryClient: QueryClient}}) {
  const user = await context.queryClient.ensureQueryData(whoamiQuery);
  if (!user.isAuthenticated) throw redirect({to: '/login'});
  if (user.mustChangePassword) throw redirect({to: '/change-password'});
  if (user.mustSetUpTwoFactor) throw redirect({to: '/set-up-two-factor'});
}

/**
 * The `/change-password` page's guard: only for someone signed in who must change their password. Anyone signed out
 * goes to sign in; anyone who needn't goes home.
 */
export async function requirePasswordChange({context}: {context: {queryClient: QueryClient}}) {
  const user = await context.queryClient.ensureQueryData(whoamiQuery);
  if (!user.isAuthenticated) throw redirect({to: '/login'});
  if (!user.mustChangePassword) throw redirect({to: '/'});
}

/**
 * The `/set-up-two-factor` page's guard: only for someone signed in who must set it up, and has no password to change
 * first. Anyone signed out goes to sign in; anyone with a password to change goes to do that; anyone else goes home.
 */
export async function requireTwoFactorSetup({context}: {context: {queryClient: QueryClient}}) {
  const user = await context.queryClient.ensureQueryData(whoamiQuery);
  if (!user.isAuthenticated) throw redirect({to: '/login'});
  if (user.mustChangePassword) throw redirect({to: '/change-password'});
  if (!user.mustSetUpTwoFactor) throw redirect({to: '/'});
}

/**
 * An admin page's `beforeLoad`: anyone who isn't an admin is sent home, as the Blazor Users page did. The API
 * refuses them anyway; this keeps them off a page that would only show errors.
 */
export async function requireAdmin({context}: {context: {queryClient: QueryClient}}) {
  const user = await context.queryClient.ensureQueryData(whoamiQuery);
  if (!user.isAdmin) throw redirect({to: '/'});
}

/** The signed-in user. Only use it under the signed-in layout, whose guard has already loaded it. */
export function useCurrentUser(): CurrentUser {
  return useSuspenseQuery(whoamiQuery).data;
}

export interface LoginInput {
  username: string;
  password: string;
}

/**
 * Goes into the app as the account now is: forgets the cached whoami, so the signed-in layout's guard asks the API
 * again, then opens the start page chosen in Account Settings (Home, the welcome page, unless changed). The guard
 * sends anyone who must still change their password to do that first.
 *
 * With `redirectTo` (a path checked by `safeRedirectPath`, from `/login?redirect=…`), it goes there instead, e.g.
 * back to `/confirm-email`; but not for an account on hold, which goes to the start page and so to its hold page.
 */
export function useEnterApp(redirectTo?: string) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return async () => {
    queryClient.removeQueries({queryKey: whoamiQuery.queryKey});
    const me = await loadAccountSettings(queryClient);
    const isHeld = !me || me.mustChangePassword || me.mustSetUpTwoFactor;
    // A checked path in this app, query and all, so by `href` rather than a route's `to`.
    if (redirectTo && !isHeld) await navigate({href: redirectTo} as Parameters<typeof navigate>[0]);
    else await navigate({to: currentStartPagePath()});
  };
}

/**
 * The account's settings, before going into the app: signing out clears them from this browser (all but the visual
 * ones), so this brings back the account's start page and the rest. Not for someone who must change their password
 * first, whom the API refuses everything else. If they can't be loaded, this browser's settings stay as they are, and
 * `SettingsSync` tries again inside the app.
 */
async function loadAccountSettings(queryClient: QueryClient): Promise<CurrentUser | null> {
  let me: CurrentUser | null = null;
  try {
    me = await queryClient.fetchQuery(whoamiQuery);
    if (!me.isAuthenticated || me.mustChangePassword) return me;
    const saved = await queryClient.fetchQuery(settingsQuery);
    if (saved) appSettings.load(saved);
  } catch {
    // Not loaded: the app starts with this browser's settings.
  }
  return me;
}

/** A password sign-in's first step for an account with two-factor authentication: the code is still to come. */
export interface TwoFactorChallenge {
  /** Names this sign-in to `POST api/auth/login/two-factor`, which ends it. */
  challenge: string;
  /** When it runs out (UTC), and the password must be given again. */
  expiresAtUtc: string;
  /** How the code comes: from an authenticator app, or emailed by MedApp. */
  method: 'Authenticator' | 'Email';
  /** For codes by email, the code just sent (where, and when another can be); null if none could be sent. */
  emailCode: EmailCodeSent | null;
}

/** How a password sign-in went: in, or (with two-factor authentication) a code still to give. */
export type LoginResult = {kind: 'signedIn'} | ({kind: 'twoFactor'} & TwoFactorChallenge);

/**
 * Signs in with a username and password, then goes to the start page, or to `redirectTo` (`useEnterApp`). For an
 * account with two-factor authentication the API answers 202 with a challenge instead, and no session yet: the result
 * is that challenge, for `useLoginTwoFactor`, and nothing navigates.
 */
export function useLogin(redirectTo?: string) {
  const enterApp = useEnterApp(redirectTo);
  return useMutation({
    mutationFn: async (body: LoginInput): Promise<LoginResult> => {
      const result = await api.POST('/api/auth/login', {body});
      if (result.response.status === 202) {
        const data = result.data as Schemas['TwoFactorChallengeResponse'] | undefined;
        const method = data?.method === 'Email' ? 'Email' : 'Authenticator';
        return {
          kind: 'twoFactor',
          challenge: data?.challenge ?? '',
          expiresAtUtc: data?.expiresAtUtc ?? '',
          method,
          emailCode: method === 'Email' && data?.emailCode ? toEmailCodeSent(data.emailCode) : null,
        };
      }
      await unwrap(Promise.resolve(result));
      return {kind: 'signedIn'};
    },
    onSuccess: async (result) => {
      if (result.kind === 'signedIn') await enterApp();
    },
  });
}

/** The second step of a two-factor sign-in: the challenge, with an authenticator code or a recovery code. */
export type LoginTwoFactorInput = {challenge: string} & ({code: string} | {recoveryCode: string});

/**
 * Finishes a two-factor sign-in with a code, then goes into the app as a password sign-in does (`useEnterApp`). A 429
 * means too many codes for the account; its title says when to try again.
 */
export function useLoginTwoFactor(redirectTo?: string) {
  const enterApp = useEnterApp(redirectTo);
  return useMutation({
    mutationFn: (body: LoginTwoFactorInput) => unwrap(api.POST('/api/auth/login/two-factor', {body})),
    onSuccess: enterApp,
  });
}

/**
 * Emails a new code for a sign-in using codes by email (`POST api/auth/login/two-factor/resend`), replacing the last:
 * at most once a minute and three times a sign-in (a 429 whose title says when). A 401 with `code: "sign_in_expired"`
 * when the sign-in has run out.
 */
export function useResendSignInCode() {
  return useMutation({
    mutationFn: async (challenge: string) =>
      toEmailCodeSent(await unwrap(api.POST('/api/auth/login/two-factor/resend', {body: {challenge}}))),
  });
}

/**
 * Signs out and goes to the login page. First, a settings change still waiting to be saved (a pin made just before)
 * is saved to the account, while the session can still save it (`flushSettings`). Local state is cleared even if the
 * API call fails, as in the Blazor app: the query cache, and the app's browser storage but for the purely visual
 * settings (`clearStorageAtSignOut`), so nothing of the user's is left on a shared computer. Both are cleared after
 * leaving the signed-in layout, so its components don't refetch, or save settings to the account, while still mounted.
 */
export function useLogout() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: async () => {
      await flushSettings();
      return api.POST('/api/auth/logout');
    },
    onSettled: async () => {
      await navigate({to: '/login'});
      queryClient.clear();
      clearStorageAtSignOut();
    },
  });
}
