import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../api/preload';
import {requirePermission} from '../../auth/auth';
import {parseSetupSearch} from '../../pages/listSearch';
import {PracticesPage, practicesPageQuery} from '../../pages/practices/PracticesPage';

/** The GP practice directory, for anyone who can view patients; holders of EditPatients keep it. */
export const Route = createFileRoute('/_app/practices')({
  beforeLoad: requirePermission('ViewPatients'),
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, practicesPageQuery),
  component: function PracticesRoute() {
    const navigate = Route.useNavigate();
    return <PracticesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
