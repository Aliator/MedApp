import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {specialtiesQuery} from '../../../api/specialties';
import {listSettingsFor} from '../../../app/listSettings';
import {can, requirePermission, whoamiQuery} from '../../../auth/auth';
import {ResultsPage, resultsListQuery} from '../../../pages/results/ResultsPage';
import {parseResultsSearch, resultsViewOf} from '../../../pages/results/resultsSearch';

/** Results awaiting sign-off and signed off, for anyone who can see patients. */
export const Route = createFileRoute('/_app/results/')({
  beforeLoad: requirePermission('ViewPatients'),
  validateSearch: parseResultsSearch,
  loaderDeps: ({search}) => search,
  loader: async ({context, deps}) => {
    const user = await context.queryClient.ensureQueryData(whoamiQuery);
    const view = resultsViewOf(deps, can(user, 'SignOffResults'));
    return preload(context.queryClient, resultsListQuery(view, deps, listSettingsFor(context.queryClient).pageSize), specialtiesQuery(true));
  },
  component: function ResultsRoute() {
    const navigate = Route.useNavigate();
    return <ResultsPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
