import {createFileRoute} from '@tanstack/react-router';
import {hospitalsQuery} from '../../../api/hospitals';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {requirePermission} from '../../../auth/auth';
import {HospitalRequestsPage, hospitalRequestsListQuery} from '../../../pages/hospitals/HospitalRequestsPage';
import {parseHospitalRequestsSearch} from '../../../pages/hospitals/hospitalRequestsSearch';

/** The hospitals' requests to the lab, for anyone who can see patients (the cases are the lab's: others see none). */
export const Route = createFileRoute('/_app/hospital-requests/')({
  beforeLoad: requirePermission('ViewPatients'),
  validateSearch: parseHospitalRequestsSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) =>
    preload(context.queryClient, hospitalRequestsListQuery(deps, listSettingsFor(context.queryClient).pageSize), hospitalsQuery(true)),
  component: function HospitalRequestsRoute() {
    const navigate = Route.useNavigate();
    return <HospitalRequestsPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
