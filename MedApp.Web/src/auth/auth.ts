import {queryOptions, useMutation, useQueryClient, useSuspenseQuery, type QueryClient} from '@tanstack/react-query';
import {redirect, useNavigate} from '@tanstack/react-router';
import {api, unwrap} from '../api/client';
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
}

const signedOut: CurrentUser = {isAuthenticated: false, username: '', roles: [], isAdmin: false, mustChangePassword: false};

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
    };
  },
  staleTime: Infinity,
});

/**
 * The signed-in layout's guard (`_app`): anyone signed out goes to sign in, and anyone who must change the password
 * they were given goes to do that, before the layout loads anything the API would refuse them.
 */
export async function requireSignedIn({context}: {context: {queryClient: QueryClient}}) {
  const user = await context.queryClient.ensureQueryData(whoamiQuery);
  if (!user.isAuthenticated) throw redirect({to: '/login'});
  if (user.mustChangePassword) throw redirect({to: '/change-password'});
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
 */
export function useEnterApp() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return async () => {
    queryClient.removeQueries({queryKey: whoamiQuery.queryKey});
    await loadAccountSettings(queryClient);
    await navigate({to: currentStartPagePath()});
  };
}

/**
 * The account's settings, before going into the app: signing out clears them from this browser (all but the visual
 * ones), so this brings back the account's start page and the rest. Not for someone who must change their password
 * first, whom the API refuses everything else. If they can't be loaded, this browser's settings stay as they are, and
 * `SettingsSync` tries again inside the app.
 */
async function loadAccountSettings(queryClient: QueryClient) {
  try {
    const me = await queryClient.fetchQuery(whoamiQuery);
    if (!me.isAuthenticated || me.mustChangePassword) return;
    const saved = await queryClient.fetchQuery(settingsQuery);
    if (saved) appSettings.load(saved);
  } catch {
    // Not loaded: the app starts with this browser's settings.
  }
}

/** Signs in, then goes to the start page (`useEnterApp`). */
export function useLogin() {
  const enterApp = useEnterApp();
  return useMutation({
    mutationFn: (body: LoginInput) => unwrap(api.POST('/api/auth/login', {body})),
    onSuccess: enterApp,
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
