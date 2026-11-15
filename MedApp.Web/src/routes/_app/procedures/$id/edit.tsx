import {createFileRoute} from '@tanstack/react-router';
import {EditProcedurePage} from '../../../../pages/procedures/EditProcedurePage';
import {preload} from '../../../../api/preload';
import {procedureQuery} from '../../../../api/procedures';

export const Route = createFileRoute('/_app/procedures/$id/edit')({
  loader: ({context, params}) => preload(context.queryClient, procedureQuery(params.id)),
  component: function EditProcedureRoute() {
    return <EditProcedurePage id={Route.useParams().id} />;
  },
});
