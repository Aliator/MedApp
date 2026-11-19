import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {can, requirePermission, whoamiQuery} from '../../../auth/auth';
import {LabPage, labListQuery} from '../../../pages/specimens/LabPage';
import {labViewOf, parseLabSearch} from '../../../pages/specimens/labSearch';

/**
 * The samples worklist, for anyone who can see patients (the side nav offers it to those who record or process
 * specimens). It was `/lab` until MC13 gave the lab its home there.
 */
export const Route = createFileRoute('/_app/lab/samples')({
  beforeLoad: requirePermission('ViewPatients'),
  validateSearch: parseLabSearch,
  loaderDeps: ({search}) => search,
  loader: async ({context, deps}) => {
    const user = await context.queryClient.ensureQueryData(whoamiQuery);
    const view = labViewOf(deps, can(user, 'ProcessSpecimens'));
    return preload(context.queryClient, labListQuery(view, deps, listSettingsFor(context.queryClient).pageSize));
  },
  component: function SamplesRoute() {
    const navigate = Route.useNavigate();
    return <LabPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
