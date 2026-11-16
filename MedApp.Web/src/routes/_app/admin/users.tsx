import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../auth/auth';
import {parseUsersSearch} from '../../../pages/listSearch';
import {UsersPage} from '../../../pages/users/UsersPage';
import {preload} from '../../../api/preload';
import {rolesQuery} from '../../../api/users';
import {featuresQuery} from '../../../api/account';
import {listSettingsFor} from '../../../app/listSettings';
import {usersListQuery} from '../../../pages/users/UsersPage';

/** Admin only. Anyone else is sent home, as the Blazor Users page does. */
export const Route = createFileRoute('/_app/admin/users')({
  beforeLoad: requireAdmin,
  validateSearch: parseUsersSearch,
  loaderDeps: ({search}) => search,
  // The list's query takes every role (an "is any of" every role is no filter), so the roles come first: waited for
  // on the server, and from the cache (if there) in the browser.
  loader: async ({context, deps}) => {
    const {queryClient} = context;
    const roles =
      typeof window === 'undefined'
        ? await queryClient.fetchQuery(rolesQuery).catch(() => undefined)
        : queryClient.getQueryData(rolesQuery.queryKey);
    const pageSize = listSettingsFor(queryClient).pageSize;
    await preload(queryClient, rolesQuery, featuresQuery, ...(roles ? [usersListQuery(deps, pageSize, roles)] : []));
  },
  component: UsersRoute,
});

function UsersRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <UsersPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
