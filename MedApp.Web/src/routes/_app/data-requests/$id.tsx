import {createFileRoute} from '@tanstack/react-router';
import {dataRequestQuery} from '../../../api/dataRequests';
import {preload} from '../../../api/preload';
import {requirePermissions} from '../../../auth/auth';
import {DataRequestPage} from '../../../pages/dataRequests/DataRequestPage';
import {lowerCaseId} from '../../../utils/routeIds';

/** One data request, for holders of ManagePrivacy and SignOffResults. */
export const Route = createFileRoute('/_app/data-requests/$id')({
  beforeLoad: requirePermissions('ViewPatients', ['ManagePrivacy', 'SignOffResults']),
  // The ID in lower case, as the API returns it, so the page and its changes share one cache entry.
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, dataRequestQuery(params.id)),
  component: function DataRequestRoute() {
    const {id} = Route.useParams();
    return <DataRequestPage key={id} id={id} />;
  },
});
