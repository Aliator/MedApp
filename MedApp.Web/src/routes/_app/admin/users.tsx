import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../auth/auth';
import {parseUsersSearch} from '../../../pages/listSearch';
import {UsersPage} from '../../../pages/users/UsersPage';

/** Admin only. Anyone else is sent home, as the Blazor Users page does. */
export const Route = createFileRoute('/_app/admin/users')({
  beforeLoad: requireAdmin,
  validateSearch: parseUsersSearch,
  component: UsersRoute,
});

function UsersRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <UsersPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
