import {createFileRoute} from '@tanstack/react-router';
import {attachmentsQuery} from '../../../../api/attachments';
import {preload} from '../../../../api/preload';
import {referralQuery} from '../../../../api/referrals';
import {ReferralPage} from '../../../../pages/referrals/ReferralPage';
import {lowerCaseId} from '../../../../utils/routeIds';

export const Route = createFileRoute('/_app/referrals/$id/')({
  // The ID in lower case, as the API returns it, so the page and its changes share one cache entry.
  params: lowerCaseId,
  // The referral, with its history, and its letter.
  loader: ({context, params}) => preload(context.queryClient, referralQuery(params.id), attachmentsQuery({referralId: params.id})),
  component: function ReferralRoute() {
    const {id} = Route.useParams();
    return <ReferralPage key={id} id={id} />;
  },
});
