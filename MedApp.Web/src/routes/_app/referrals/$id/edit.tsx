import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../../api/preload';
import {referralQuery} from '../../../../api/referrals';
import {specialtiesQuery} from '../../../../api/specialties';
import {EditReferralPage} from '../../../../pages/referrals/EditReferralPage';
import {lowerCaseId} from '../../../../utils/routeIds';

export const Route = createFileRoute('/_app/referrals/$id/edit')({
  // The ID in lower case, as the API returns it, so the page and its saves share one cache entry.
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, referralQuery(params.id), specialtiesQuery(false)),
  component: function EditReferralRoute() {
    const {id} = Route.useParams();
    return <EditReferralPage key={id} id={id} />;
  },
});
