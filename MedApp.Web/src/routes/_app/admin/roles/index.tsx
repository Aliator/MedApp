import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {parseSetupSearch} from '../../../../pages/listSearch';
import {RolesPage} from '../../../../pages/roles/RolesPage';
import {preload} from '../../../../api/preload';
import {rolesQuery} from '../../../../api/roles';

/** For holders of ManageRoles (the built-in Admin role); anyone else is told the page needs it. */
export const Route = createFileRoute('/_app/admin/roles/')({
  beforeLoad: requirePermission('ManageRoles'),
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, rolesQuery),
  component: function RolesRoute() {
    const navigate = Route.useNavigate();
    return <RolesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
