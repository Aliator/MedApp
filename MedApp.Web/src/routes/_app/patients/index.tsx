import {createFileRoute} from '@tanstack/react-router';
import {parsePatientsSearch} from '../../../pages/listSearch';
import {PatientsListPage} from '../../../pages/patients/PatientsListPage';

export const Route = createFileRoute('/_app/patients/')({
  validateSearch: parsePatientsSearch,
  component: PatientsRoute,
});

function PatientsRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <PatientsListPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
