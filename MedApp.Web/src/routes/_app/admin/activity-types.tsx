import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../auth/auth';
import {parseSetupSearch} from '../../../pages/listSearch';
import {ActivityTypesPage} from '../../../pages/setup/ActivityTypesPage';
import {preload} from '../../../api/preload';
import {activityTypesQuery} from '../../../api/calendar';

export const Route = createFileRoute('/_app/admin/activity-types')({
  beforeLoad: requirePermission('ManageSetup'),
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, activityTypesQuery),
  component: function ActivityTypesRoute() {
    const navigate = Route.useNavigate();
    return <ActivityTypesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
