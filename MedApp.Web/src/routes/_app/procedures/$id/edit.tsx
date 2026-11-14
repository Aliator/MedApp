import {createFileRoute} from '@tanstack/react-router';
import {EditProcedurePage} from '../../../../pages/procedures/EditProcedurePage';

export const Route = createFileRoute('/_app/procedures/$id/edit')({
  component: function EditProcedureRoute() {
    return <EditProcedurePage id={Route.useParams().id} />;
  },
});
