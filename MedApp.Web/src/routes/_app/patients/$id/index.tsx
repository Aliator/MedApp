import {createFileRoute} from '@tanstack/react-router';
import {ViewPatientPage} from '../../../../pages/patients/ViewPatientPage';
import {preload} from '../../../../api/preload';
import {patientQuery} from '../../../../api/patients';

export const Route = createFileRoute('/_app/patients/$id/')({
  loader: ({context, params}) => preload(context.queryClient, patientQuery(params.id)),
  component: function ViewPatientRoute() {
    return <ViewPatientPage id={Route.useParams().id} />;
  },
});
