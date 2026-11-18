import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {preload} from '../../../../api/preload';
import {resultQuery} from '../../../../api/results';
import {ResultPage} from '../../../../pages/results/ResultPage';
import {lowerCaseId} from '../../../../utils/routeIds';

/** One result, for anyone who can see patients (opening it is recorded as viewing it). */
export const Route = createFileRoute('/_app/results/$id/')({
  beforeLoad: requirePermission('ViewPatients'),
  // The ID in lower case, as the API returns it, so the page and its changes share one cache entry.
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, resultQuery(params.id)),
  component: function ResultRoute() {
    const {id} = Route.useParams();
    return <ResultPage key={id} id={id} />;
  },
});
