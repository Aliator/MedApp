import {createFileRoute} from '@tanstack/react-router';
import {ViewPatientPage} from '../../../../pages/patients/ViewPatientPage';
import {preload} from '../../../../api/preload';
import {patientQuery} from '../../../../api/patients';
import {whoamiQuery} from '../../../../auth/auth';
import {patientAccessQuery} from '../../../../components/patients/PatientAccessReport';

export const Route = createFileRoute('/_app/patients/$id/')({
  loader: ({context, params}) => {
    // Admins also see the record's access report.
    const isAdmin = context.queryClient.getQueryData(whoamiQuery.queryKey)?.isAdmin === true;
    return preload(context.queryClient, patientQuery(params.id), ...(isAdmin ? [patientAccessQuery(params.id)] : []));
  },
  component: function ViewPatientRoute() {
    return <ViewPatientPage id={Route.useParams().id} />;
  },
});
