import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../auth/auth';
import {preload} from '../../../api/preload';
import {allTemplatesQuery} from '../../../api/procedureSetup';
import {parseInsightsSearch} from '../../../pages/listSearch';
import {InsightsPage} from '../../../pages/insights/InsightsPage';
import {insightsPageQuery} from '../../../pages/insights/insightsPageQuery';

/** Admin only: an overview of the procedure work. */
export const Route = createFileRoute('/_app/admin/insights')({
  beforeLoad: requireAdmin,
  validateSearch: parseInsightsSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => preload(context.queryClient, insightsPageQuery(deps), allTemplatesQuery),
  component: function InsightsRoute() {
    const navigate = Route.useNavigate();
    return <InsightsPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
