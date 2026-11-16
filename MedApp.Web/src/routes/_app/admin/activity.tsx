import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../auth/auth';
import {parseActivitySearch} from '../../../pages/listSearch';
import {ActivityLogPage, activityListQuery} from '../../../pages/activity/ActivityLogPage';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';

export const Route = createFileRoute('/_app/admin/activity')({
  beforeLoad: requireAdmin,
  validateSearch: parseActivitySearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => preload(context.queryClient, activityListQuery(deps, listSettingsFor(context.queryClient).pageSize)),
  component: function ActivityLogRoute() {
    const navigate = Route.useNavigate();
    return <ActivityLogPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
