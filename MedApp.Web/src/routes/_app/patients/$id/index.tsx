import {createFileRoute} from '@tanstack/react-router';
import {ViewPatientPage} from '../../../../pages/patients/ViewPatientPage';

export const Route = createFileRoute('/_app/patients/$id/')({
  component: function ViewPatientRoute() {
    return <ViewPatientPage id={Route.useParams().id} />;
  },
});
