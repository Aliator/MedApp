import {queryOptions, useMutation, useQueryClient, useSuspenseQuery, type QueryClient} from '@tanstack/react-query';
import {redirect, useNavigate} from '@tanstack/react-router';
import {api, unwrap} from '../api/client';
import {useNavSettings} from '../app/navSettings';

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
  const {startPagePath} = useNavSettings();
  return async () => {
    queryClient.removeQueries({queryKey: whoamiQuery.queryKey});
    await navigate({to: startPagePath});
  };
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
 * Signs out and goes to the login page. Local state is cleared even if the API call fails, as in the Blazor app.
 * The cache is cleared after leaving the signed-in layout, so its components don't refetch while still mounted.
 */
export function useLogout() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: () => api.POST('/api/auth/logout'),
    onSettled: async () => {
      await navigate({to: '/login'});
      queryClient.clear();
    },
  });
}
