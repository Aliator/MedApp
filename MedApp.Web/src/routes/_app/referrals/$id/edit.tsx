import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {preload} from '../../../../api/preload';
import {referralQuery} from '../../../../api/referrals';
import {teamsQuery} from '../../../../api/teams';
import {EditReferralPage} from '../../../../pages/referrals/EditReferralPage';
import {lowerCaseId} from '../../../../utils/routeIds';

export const Route = createFileRoute('/_app/referrals/$id/edit')({
  beforeLoad: requirePermission('RegisterReferrals'),
  // The ID in lower case, as the API returns it, so the page and its saves share one cache entry.
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, referralQuery(params.id), teamsQuery(false)),
  component: function EditReferralRoute() {
    const {id} = Route.useParams();
    return <EditReferralPage key={id} id={id} />;
  },
});
