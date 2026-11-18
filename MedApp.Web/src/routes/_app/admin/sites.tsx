import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {requireAdmin} from '../../../auth/auth';
import {parseSetupSearch} from '../../../pages/listSearch';
import {SitesPage, sitesPageQuery} from '../../../pages/setup/SitesPage';

/** Admin only: the places the centre works from, and their rooms. */
export const Route = createFileRoute('/_app/admin/sites')({
  beforeLoad: requireAdmin,
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, sitesPageQuery),
  component: function SitesRoute() {
    const navigate = Route.useNavigate();
    return <SitesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
