import {createFileRoute} from '@tanstack/react-router';
import {EditPatientPage} from '../../../../pages/patients/EditPatientPage';
import {preload} from '../../../../api/preload';
import {patientQuery} from '../../../../api/patients';

export const Route = createFileRoute('/_app/patients/$id/edit')({
  loader: ({context, params}) => preload(context.queryClient, patientQuery(params.id)),
  component: function EditPatientRoute() {
    return <EditPatientPage id={Route.useParams().id} />;
  },
});
