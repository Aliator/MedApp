import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {EditPathwayPage} from '../../../../pages/pathways/EditPathwayPage';
import {preload} from '../../../../api/preload';
import {pathwayQuery} from '../../../../api/pathways';
import {lowerCaseId} from '../../../../utils/routeIds';

export const Route = createFileRoute('/_app/pathways/$id/edit')({
  beforeLoad: requirePermission('WorkPathways'),
  // The ID in lower case, as the API returns it, so the page and its saves share one cache entry.
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, pathwayQuery(params.id)),
  component: function EditPathwayRoute() {
    return <EditPathwayPage id={Route.useParams().id} />;
  },
});
