import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {requireAdmin} from '../../../auth/auth';
import {ClosedDaysPage, closedDaysPageQuery, parseClosedDaysSearch} from '../../../pages/setup/ClosedDaysPage';

/** Admin only: the days the clinic is closed (bank holidays and the like), a year at a time. */
export const Route = createFileRoute('/_app/admin/closed-days')({
  beforeLoad: requireAdmin,
  validateSearch: parseClosedDaysSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => preload(context.queryClient, closedDaysPageQuery(deps)),
  component: function ClosedDaysRoute() {
    const navigate = Route.useNavigate();
    return <ClosedDaysPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
