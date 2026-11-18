import {createFileRoute} from '@tanstack/react-router';
import {PathwayPage} from '../../../../pages/pathways/PathwayPage';
import {attachmentsQuery} from '../../../../api/attachments';
import {preload} from '../../../../api/preload';
import {pathwayQuery} from '../../../../api/pathways';
import {lowerCaseId} from '../../../../utils/routeIds';
import {pathwayAppointmentsQuery} from '../../../../api/appointments';
import {parsePathwaySearch} from '../../../../pages/pathways/pathwaySearch';

export const Route = createFileRoute('/_app/pathways/$id/')({
  // The ID in lower case, as the API returns it, so the page and its saves share one cache entry.
  params: lowerCaseId,
  // A request to delete one of its notes, opened for an admin (from their notification).
  validateSearch: parsePathwaySearch,
  // The pathway, its appointments (its stages' visits and the Appointments list), and its own files (open beside it
  // as the page opens; a stage's are fetched when opened).
  loader: ({context, params}) =>
    preload(context.queryClient, pathwayQuery(params.id), pathwayAppointmentsQuery(params.id), attachmentsQuery({pathwayId: params.id})),
  component: function PathwayRoute() {
    const {id} = Route.useParams();
    const {deletionRequest} = Route.useSearch();
    const navigate = Route.useNavigate();
    // Keyed by pathway, so moving to another one starts a fresh visit (the "not assigned to you" check asks again).
    return (
      <PathwayPage
        key={id}
        id={id}
        deletionRequest={deletionRequest}
        onCloseDeletionRequest={() => void navigate({search: {}, replace: true, resetScroll: false})}
        onDeletionRequestElsewhere={(pathwayId, requestId) =>
          void navigate({to: '/pathways/$id', params: {id: pathwayId}, search: {deletionRequest: requestId}, replace: true})
        }
      />
    );
  },
});
