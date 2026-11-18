import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../auth/auth';
import {MdtPage, preloadMdtPage} from '../../pages/mdt/MdtPage';
import {parseMdtSearch} from '../../pages/mdt/mdtSearch';

/** The MDT list, for anyone who can view patients; holders of WorkPathways and RecordMdtOutcomes change items. */
export const Route = createFileRoute('/_app/mdt')({
  beforeLoad: requirePermission('ViewPatients'),
  validateSearch: parseMdtSearch,
  loaderDeps: ({search}) => ({team: search.team}),
  loader: ({context, deps}) => preloadMdtPage(context.queryClient, deps),
  component: function MdtRoute() {
    const navigate = Route.useNavigate();
    return <MdtPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
