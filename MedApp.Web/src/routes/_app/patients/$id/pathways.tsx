import {createFileRoute} from '@tanstack/react-router';
import {parsePathwaysSearch} from '../../../../pages/listSearch';
import {PathwaysListPage} from '../../../../pages/pathways/PathwaysListPage';
import {preload} from '../../../../api/preload';
import {patientQuery} from '../../../../api/patients';
import {listSettingsFor} from '../../../../app/listSettings';
import {pathwaysListQuery} from '../../../../pages/pathways/PathwaysListPage';

export const Route = createFileRoute('/_app/patients/$id/pathways')({
  validateSearch: parsePathwaysSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps, params}) =>
    preload(context.queryClient, patientQuery(params.id), pathwaysListQuery(deps, listSettingsFor(context.queryClient), params.id)),
  component: PatientPathwaysRoute,
});

function PatientPathwaysRoute() {
  const {id} = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <PathwaysListPage patientId={id} search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
