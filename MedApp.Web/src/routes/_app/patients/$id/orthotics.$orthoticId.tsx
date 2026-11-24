import {createFileRoute} from '@tanstack/react-router';
import {OrthoticPage} from '../../../../pages/patients/OrthoticPage';

export const Route = createFileRoute('/_app/patients/$id/orthotics/$orthoticId')({
  component: function OrthoticRoute() {
    return <OrthoticPage patientId={Route.useParams().id} />;
  },
});
