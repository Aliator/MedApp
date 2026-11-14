import {createFileRoute} from '@tanstack/react-router';
import {EditPatientPage} from '../../../../pages/patients/EditPatientPage';

export const Route = createFileRoute('/_app/patients/$id/edit')({
  component: function EditPatientRoute() {
    return <EditPatientPage id={Route.useParams().id} />;
  },
});
