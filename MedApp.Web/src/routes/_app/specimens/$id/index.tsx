import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {preload} from '../../../../api/preload';
import {specimenQuery} from '../../../../api/specimens';
import {SpecimenPage} from '../../../../pages/specimens/SpecimenPage';
import {lowerCaseId} from '../../../../utils/routeIds';

/** One specimen, for anyone who can see patients (opening it is recorded as viewing it). */
export const Route = createFileRoute('/_app/specimens/$id/')({
  beforeLoad: requirePermission('ViewPatients'),
  // The ID in lower case, as the API returns it, so the page and its changes share one cache entry.
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, specimenQuery(params.id)),
  component: function SpecimenRoute() {
    const {id} = Route.useParams();
    return <SpecimenPage key={id} id={id} />;
  },
});
