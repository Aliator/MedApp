import {createFileRoute} from '@tanstack/react-router';
import {labCaseQuery} from '../../../../api/labCases';
import {preload} from '../../../../api/preload';
import {requirePermission} from '../../../../auth/auth';
import {LabCasePage} from '../../../../pages/labCases/LabCasePage';
import {lowerCaseId} from '../../../../utils/routeIds';

/** One lab case: its steps, outcomes, samples and report (opening it is recorded as viewing it). */
export const Route = createFileRoute('/_app/lab/cases/$id')({
  beforeLoad: requirePermission('ViewPatients'),
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, labCaseQuery(params.id)),
  component: function LabCaseRoute() {
    const {id} = Route.useParams();
    return <LabCasePage key={id} id={id} />;
  },
});
