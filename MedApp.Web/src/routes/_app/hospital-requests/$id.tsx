import {createFileRoute} from '@tanstack/react-router';
import {hospitalRequestQuery} from '../../../api/hospitals';
import {preload} from '../../../api/preload';
import {requirePermission} from '../../../auth/auth';
import {HospitalRequestPage} from '../../../pages/hospitals/HospitalRequestPage';
import {lowerCaseId} from '../../../utils/routeIds';

/** One hospital request and its lab case (opening it is recorded as viewing the case). */
export const Route = createFileRoute('/_app/hospital-requests/$id')({
  beforeLoad: requirePermission('ViewPatients'),
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, hospitalRequestQuery(params.id)),
  component: function HospitalRequestRoute() {
    const {id} = Route.useParams();
    return <HospitalRequestPage key={id} id={id} />;
  },
});
