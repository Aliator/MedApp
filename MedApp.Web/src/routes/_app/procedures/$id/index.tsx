import {createFileRoute} from '@tanstack/react-router';
import {ProcedurePage} from '../../../../pages/procedures/ProcedurePage';
import {attachmentsQuery} from '../../../../api/attachments';
import {preload} from '../../../../api/preload';
import {procedureQuery} from '../../../../api/procedures';
import {lowerCaseId} from '../../../../utils/routeIds';
import {procedureAppointmentsQuery} from '../../../../api/appointments';
import {parseProcedureSearch} from '../../../../pages/procedures/procedureSearch';

export const Route = createFileRoute('/_app/procedures/$id/')({
  // The ID in lower case, as the API returns it, so the page and its saves share one cache entry.
  params: lowerCaseId,
  // A request to delete one of its notes, opened for an admin (from their notification).
  validateSearch: parseProcedureSearch,
  // The procedure, its appointments (its stages' visits and the Appointments list), and its own files (open beside it
  // as the page opens; a stage's are fetched when opened).
  loader: ({context, params}) =>
    preload(context.queryClient, procedureQuery(params.id), procedureAppointmentsQuery(params.id), attachmentsQuery({procedureId: params.id})),
  component: function ProcedureRoute() {
    const {id} = Route.useParams();
    const {deletionRequest} = Route.useSearch();
    const navigate = Route.useNavigate();
    // Keyed by procedure, so moving to another one starts a fresh visit (the "not assigned to you" check asks again).
    return (
      <ProcedurePage
        key={id}
        id={id}
        deletionRequest={deletionRequest}
        onCloseDeletionRequest={() => void navigate({search: {}, replace: true, resetScroll: false})}
      />
    );
  },
});
