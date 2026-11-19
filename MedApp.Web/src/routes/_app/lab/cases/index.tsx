import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../../api/preload';
import {listSettingsFor} from '../../../../app/listSettings';
import {requirePermission} from '../../../../auth/auth';
import {LabCasesPage, labCasesListQuery} from '../../../../pages/labCases/LabCasesPage';
import {parseLabCasesSearch} from '../../../../pages/labCases/labCasesSearch';

/** The lab's cases (MC15), for anyone who can see patients: the lab sees every one, a clinic its own. */
export const Route = createFileRoute('/_app/lab/cases/')({
  beforeLoad: requirePermission('ViewPatients'),
  validateSearch: parseLabCasesSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => preload(context.queryClient, labCasesListQuery(deps, listSettingsFor(context.queryClient).pageSize)),
  component: function LabCasesRoute() {
    const navigate = Route.useNavigate();
    return <LabCasesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
