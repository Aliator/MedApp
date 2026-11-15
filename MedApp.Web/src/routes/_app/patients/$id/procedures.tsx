import {createFileRoute} from '@tanstack/react-router';
import {parseProceduresSearch} from '../../../../pages/listSearch';
import {ProceduresListPage} from '../../../../pages/procedures/ProceduresListPage';
import {preload} from '../../../../api/preload';
import {patientQuery} from '../../../../api/patients';
import {listSettingsFor} from '../../../../app/listSettings';
import {proceduresListQuery} from '../../../../pages/procedures/ProceduresListPage';

export const Route = createFileRoute('/_app/patients/$id/procedures')({
  validateSearch: parseProceduresSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps, params}) =>
    preload(context.queryClient, patientQuery(params.id), proceduresListQuery(deps, listSettingsFor(context.queryClient), params.id)),
  component: PatientProceduresRoute,
});

function PatientProceduresRoute() {
  const {id} = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <ProceduresListPage patientId={id} search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
