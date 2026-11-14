import {createFileRoute} from '@tanstack/react-router';
import {parseProceduresSearch} from '../../../../pages/listSearch';
import {ProceduresListPage} from '../../../../pages/procedures/ProceduresListPage';

export const Route = createFileRoute('/_app/patients/$id/procedures')({
  validateSearch: parseProceduresSearch,
  component: PatientProceduresRoute,
});

function PatientProceduresRoute() {
  const {id} = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <ProceduresListPage patientId={id} search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
