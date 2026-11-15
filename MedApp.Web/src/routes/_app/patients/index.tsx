import {createFileRoute} from '@tanstack/react-router';
import {parsePatientsSearch} from '../../../pages/listSearch';
import {PatientsListPage} from '../../../pages/patients/PatientsListPage';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {patientsListQuery} from '../../../pages/patients/PatientsListPage';

export const Route = createFileRoute('/_app/patients/')({
  validateSearch: parsePatientsSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => preload(context.queryClient, patientsListQuery(deps, listSettingsFor(context.queryClient).pageSize)),
  component: PatientsRoute,
});

function PatientsRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <PatientsListPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
