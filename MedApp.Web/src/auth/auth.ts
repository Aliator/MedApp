import {queryOptions, useMutation, useQueryClient, useSuspenseQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {api, unwrap} from '../api/client';

export interface CurrentUser {
  isAuthenticated: boolean;
  username: string;
  roles: string[];
  isAdmin: boolean;
}

const signedOut: CurrentUser = {isAuthenticated: false, username: '', roles: [], isAdmin: false};

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
    };
  },
  staleTime: Infinity,
});

/** The signed-in user. Only use it under the signed-in layout, whose guard has already loaded it. */
export function useCurrentUser(): CurrentUser {
  return useSuspenseQuery(whoamiQuery).data;
}

export interface LoginInput {
  username: string;
  password: string;
}

/** Signs in, then goes to the patients list, as the Blazor app does. */
export function useLogin() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (body: LoginInput) => unwrap(api.POST('/api/auth/login', {body})),
    onSuccess: async () => {
      // Forget the signed-out whoami so the guard asks the API again.
      queryClient.removeQueries({queryKey: whoamiQuery.queryKey});
      await navigate({to: '/patients'});
    },
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
