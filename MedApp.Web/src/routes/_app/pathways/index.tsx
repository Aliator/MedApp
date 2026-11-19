import {createFileRoute} from '@tanstack/react-router';
import {parsePathwaysSearch} from '../../../pages/listSearch';
import {PathwaysListPage} from '../../../pages/pathways/PathwaysListPage';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {pathwaysListQuery} from '../../../pages/pathways/PathwaysListPage';
import {clinicFor} from '../../../components/teams/ClinicPicker';

export const Route = createFileRoute('/_app/pathways/')({
  validateSearch: parsePathwaysSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) =>
    preload(
      context.queryClient,
      pathwaysListQuery(deps, listSettingsFor(context.queryClient), undefined, clinicFor(context.queryClient, 'pathways', deps.clinic)),
    ),
  component: PathwaysRoute,
});

function PathwaysRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <PathwaysListPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
