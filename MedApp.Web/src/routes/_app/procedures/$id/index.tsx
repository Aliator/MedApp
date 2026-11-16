import {createFileRoute} from '@tanstack/react-router';
import {ProcedurePage} from '../../../../pages/procedures/ProcedurePage';
import {attachmentsQuery} from '../../../../api/attachments';
import {preload} from '../../../../api/preload';
import {procedureQuery} from '../../../../api/procedures';
import {procedureAppointmentsQuery} from '../../../../api/appointments';

export const Route = createFileRoute('/_app/procedures/$id/')({
  // The procedure, its appointments (its stages' visits and the Appointments list), and its own files (open beside it
  // as the page opens; a stage's are fetched when opened).
  loader: ({context, params}) =>
    preload(context.queryClient, procedureQuery(params.id), procedureAppointmentsQuery(params.id), attachmentsQuery({procedureId: params.id})),
  component: function ProcedureRoute() {
    const {id} = Route.useParams();
    // Keyed by procedure, so moving to another one starts a fresh visit (the "not assigned to you" check asks again).
    return <ProcedurePage key={id} id={id} />;
  },
});
