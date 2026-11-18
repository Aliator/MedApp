import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {requirePermission, whoamiQuery} from '../../../auth/auth';
import {LettersPage} from '../../../pages/letters/LettersPage';
import {defaultLetterView, lettersListQuery, parseLettersSearch} from '../../../pages/letters/lettersSearch';

/** Letters: the views of letters to write, approve and send, for anyone who can view patients. */
export const Route = createFileRoute('/_app/letters/')({
  beforeLoad: requirePermission('ViewPatients'),
  validateSearch: parseLettersSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => {
    const permissions = context.queryClient.getQueryData(whoamiQuery.queryKey)?.permissions ?? [];
    const view = deps.view ?? defaultLetterView(permissions);
    return preload(context.queryClient, lettersListQuery(view, deps.page ?? 1, listSettingsFor(context.queryClient).pageSize));
  },
  component: function LettersRoute() {
    const navigate = Route.useNavigate();
    return <LettersPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
