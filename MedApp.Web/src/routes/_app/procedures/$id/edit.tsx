import {createFileRoute} from '@tanstack/react-router';
import {EditProcedurePage} from '../../../../pages/procedures/EditProcedurePage';
import {preload} from '../../../../api/preload';
import {procedureQuery} from '../../../../api/procedures';
import {lowerCaseId} from '../../../../utils/routeIds';

export const Route = createFileRoute('/_app/procedures/$id/edit')({
  // The ID in lower case, as the API returns it, so the page and its saves share one cache entry.
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, procedureQuery(params.id)),
  component: function EditProcedureRoute() {
    return <EditProcedurePage id={Route.useParams().id} />;
  },
});
