import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {requirePermissions} from '../../../auth/auth';
import {DataRequestsPage} from '../../../pages/dataRequests/DataRequestsPage';
import {DEFAULT_DATA_REQUEST_VIEW, dataRequestsListQuery, parseDataRequestsSearch} from '../../../pages/dataRequests/dataRequestsSearch';

/** Patients' data requests, for holders of ManagePrivacy (who answer them) and SignOffResults (who review them). */
export const Route = createFileRoute('/_app/data-requests/')({
  beforeLoad: requirePermissions('ViewPatients', ['ManagePrivacy', 'SignOffResults']),
  validateSearch: parseDataRequestsSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) =>
    preload(context.queryClient, dataRequestsListQuery(deps.view ?? DEFAULT_DATA_REQUEST_VIEW, deps.page ?? 1, listSettingsFor(context.queryClient).pageSize)),
  component: function DataRequestsRoute() {
    const navigate = Route.useNavigate();
    return <DataRequestsPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
