import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../auth/auth';
import {parseSetupSearch} from '../../../pages/listSearch';
import {TeamsPage, teamsPageQuery} from '../../../pages/setup/TeamsPage';
import {preload} from '../../../api/preload';

export const Route = createFileRoute('/_app/admin/teams')({
  beforeLoad: requirePermission('ManageSetup'),
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, teamsPageQuery),
  component: function TeamsRoute() {
    const navigate = Route.useNavigate();
    return <TeamsPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
