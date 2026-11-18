import {createFileRoute} from '@tanstack/react-router';
import {letterQuery} from '../../../api/letters';
import {preload} from '../../../api/preload';
import {requirePermission} from '../../../auth/auth';
import {LetterPage} from '../../../pages/letters/LetterPage';
import {lowerCaseId} from '../../../utils/routeIds';

/** One letter: its text, recipients and where it is, for anyone who can view patients. */
export const Route = createFileRoute('/_app/letters/$id')({
  beforeLoad: requirePermission('ViewPatients'),
  // The ID in lower case, as the API returns it, so the page and its changes share one cache entry.
  params: lowerCaseId,
  loader: ({context, params}) => preload(context.queryClient, letterQuery(params.id)),
  component: function LetterRoute() {
    const {id} = Route.useParams();
    return <LetterPage key={id} id={id} />;
  },
});
